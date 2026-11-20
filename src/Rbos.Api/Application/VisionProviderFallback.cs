using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rbos.Api.Application;

/// <summary>One bounded, privacy-safe direct vision-provider attempt.</summary>
public sealed record VisionProviderAttempt(
    string Provider,
    string Model,
    int TimeoutSeconds,
    Func<CancellationToken, Task<JsonNode?>> Execute);

/// <summary>
/// Runs provider attempts in their declared order. A response is accepted only when the
/// feature-specific validator says it contains useful structured data.
/// </summary>
public static class VisionProviderFallback
{
    public static int GeminiTimeoutSeconds(IConfiguration cfg) =>
        Math.Clamp(cfg.GetValue("AiPerformance:GeminiAttemptTimeoutSeconds", 25), 5, 60);

    public static int GroqTimeoutSeconds(IConfiguration cfg) =>
        Math.Clamp(cfg.GetValue("AiPerformance:GroqAttemptTimeoutSeconds", 18), 5, 45);

    public static IReadOnlyList<string> InvoiceGeminiModels(IConfiguration cfg) =>
        Models(
            cfg["Gemini:InvoiceModel"] ?? cfg["Gemini:Model"],
            cfg["Gemini:InvoiceFallbackModel"] ?? cfg["Gemini:FallbackModel"],
            cfg["Gemini:InvoiceLastResortModel"] ?? cfg["Gemini:LastResortModel"]);

    public static IReadOnlyList<string> ItemTagGeminiModels(IConfiguration cfg) =>
        Models(
            cfg["Gemini:ItemTagModel"] ?? cfg["Gemini:Model"],
            cfg["Gemini:ItemTagFallbackModel"] ?? cfg["Gemini:InvoiceFallbackModel"] ?? cfg["Gemini:FallbackModel"],
            cfg["Gemini:ItemTagLastResortModel"] ?? cfg["Gemini:InvoiceLastResortModel"] ?? cfg["Gemini:LastResortModel"]);

    static IReadOnlyList<string> Models(string? primary, string? secondary, string? tertiary)
    {
        // These old aliases appeared in the former fallback chain. Do not silently keep
        // retrying them when no explicit, supported model is configured.
        static bool Obsolete(string? model) =>
            string.Equals(model?.Trim(), "gemini-flash-latest", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(model?.Trim(), "gemini-2.5-flash-lite", StringComparison.OrdinalIgnoreCase);

        var values = new[]
        {
            string.IsNullOrWhiteSpace(primary) || Obsolete(primary) ? "gemini-4-hydrogen" : primary.Trim(),
            string.IsNullOrWhiteSpace(secondary) || Obsolete(secondary) ? "gemini-3.8-flash" : secondary.Trim(),
            string.IsNullOrWhiteSpace(tertiary) || Obsolete(tertiary) ? "gemini-3.5-flash-lite" : tertiary.Trim()
        };
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static async Task<JsonNode?> ExecuteAsync(
        string feature,
        IReadOnlyList<VisionProviderAttempt> attempts,
        Func<JsonNode?, bool> isUsable,
        ILogger logger,
        CancellationToken ct)
    {
        var unavailableProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attempt in attempts)
        {
            ct.ThrowIfCancellationRequested();
            if (unavailableProviders.Contains(attempt.Provider))
                continue;
            var timer = Stopwatch.StartNew();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(attempt.TimeoutSeconds, 5, 60)));

            try
            {
                var result = await attempt.Execute(timeout.Token);
                timer.Stop();
                if (result is not null && isUsable(result))
                {
                    logger.LogInformation(
                        "Vision extraction succeeded. Feature {Feature}, provider {Provider}, model {Model}, latency {LatencyMs}ms",
                        feature, attempt.Provider, attempt.Model, timer.ElapsedMilliseconds);
                    return result;
                }

                logger.LogWarning(
                    "Vision provider returned empty or unusable structured output. Feature {Feature}, provider {Provider}, model {Model}, latency {LatencyMs}ms",
                    feature, attempt.Provider, attempt.Model, timer.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                timer.Stop();
                logger.LogWarning(
                    "Vision provider attempt timed out. Feature {Feature}, provider {Provider}, model {Model}, timeout {TimeoutSeconds}s, latency {LatencyMs}ms",
                    feature, attempt.Provider, attempt.Model, attempt.TimeoutSeconds, timer.ElapsedMilliseconds);
            }
            catch (HttpRequestException e)
            {
                timer.Stop();
                logger.LogWarning(
                    "Vision provider attempt failed. Feature {Feature}, provider {Provider}, model {Model}, HTTP {StatusCode}, latency {LatencyMs}ms",
                    feature, attempt.Provider, attempt.Model, (int?)e.StatusCode, timer.ElapsedMilliseconds);

                // Credential/permission/billing errors apply to every model under this provider.
                // Do not spend the request budget repeating the same bad key on fallback models.
                if (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    unavailableProviders.Add(attempt.Provider);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                timer.Stop();
                logger.LogWarning(
                    "Vision provider attempt returned an invalid response. Feature {Feature}, provider {Provider}, model {Model}, error type {ErrorType}, latency {LatencyMs}ms",
                    feature, attempt.Provider, attempt.Model, e.GetType().Name, timer.ElapsedMilliseconds);
            }
        }

        logger.LogWarning("Vision provider chain exhausted. Feature {Feature}, attempt count {AttemptCount}", feature, attempts.Count);
        return null;
    }
}
