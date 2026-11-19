using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rbos.Api.Application;

public sealed class AiGateway(
    IConfiguration configuration,
    AiGatewayCredentialPool credentials,
    IEnumerable<IAiProviderAdapter> providerAdapters,
    IAiGatewayUsageLedger usageLedger,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AiGateway> log)
{
    private readonly IReadOnlyDictionary<AiProviderKind, IAiProviderAdapter> _adapters =
        providerAdapters.ToDictionary(x => x.Provider);

    public bool Enabled => configuration.GetValue("AiGateway:Enabled", false);

    public bool IsFeatureEnabled(string feature) =>
        Enabled && configuration.GetValue($"AiGateway:Features:{feature}:Enabled", true);

    public bool IsFeatureAvailable(
        string feature, IReadOnlyList<AiProviderKind> providers, AiCapability capability,
        IReadOnlyDictionary<AiProviderKind, IReadOnlyList<string>>? models = null)
    {
        if (!IsFeatureEnabled(feature)) return false;
        var allowPaid = configuration.GetValue("AiGateway:AllowPaidProviders", false);
        return providers.Any(provider =>
            _adapters.TryGetValue(provider, out var adapter) && adapter.Supports(capability) &&
            credentials.IsFeatureProviderAvailable(provider, capability, allowPaid,
                models is not null && models.TryGetValue(provider, out var configured) ? configured : null));
    }

    public async Task<AiGatewayResponse?> ExecuteAsync(AiGatewayRequest request, CancellationToken cancellationToken)
    {
        if (!IsFeatureEnabled(request.Feature) || request.TenantId == Guid.Empty ||
            request.ActorUserId == Guid.Empty || request.ProviderOrder.Count == 0)
            return null;

        if (request.Task == AiTaskKind.AudioTranscription &&
            (request.Audio is null || request.Media.Count > 0))
            return null;

        var allowPaid = configuration.GetValue("AiGateway:AllowPaidProviders", false);
        var maxAttempts = Math.Clamp(request.MaxAttemptsPerProvider, 1, 5);
        // Prefer the HTTP correlation ID established by Program.cs so API, extraction,
        // gateway and provider-attempt logs can be joined in one Cloud Run query.
        var httpCorrelationId = httpContextAccessor.HttpContext?.TraceIdentifier;
        var correlationId = !string.IsNullOrWhiteSpace(request.CorrelationId)
            ? request.CorrelationId.Trim()[..Math.Min(request.CorrelationId.Trim().Length, 64)]
            : !string.IsNullOrWhiteSpace(httpCorrelationId)
                ? httpCorrelationId[..Math.Min(httpCorrelationId.Length, 64)]
                : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        request = request with { CorrelationId = correlationId };
        var paidBudgetBlocked = false;

        foreach (var provider in request.ProviderOrder.Distinct())
        {
            if (!_adapters.TryGetValue(provider, out var adapter))
            {
                log.LogWarning("AI gateway skipped provider {Provider}: no adapter is registered; feature {Feature}; correlation {CorrelationId}",
                    provider, request.Feature, correlationId);
                continue;
            }
            if (!adapter.Supports(request.RequiredCapability))
            {
                log.LogWarning("AI gateway skipped provider {Provider}: capability {Capability} is unsupported; feature {Feature}; correlation {CorrelationId}",
                    provider, request.RequiredCapability, request.Feature, correlationId);
                continue;
            }
            if (credentials.IsCircuitOpen(provider))
            {
                log.LogWarning("AI gateway skipped provider {Provider}: circuit breaker is open; feature {Feature}; correlation {CorrelationId}",
                    provider, request.Feature, correlationId);
                continue;
            }

            var models = ResolveModels(request, provider);
            foreach (var model in models)
            {
                if (string.IsNullOrWhiteSpace(model)) continue;
                var candidates = credentials.SelectOrder(provider, request.RequiredCapability, model, allowPaid);
                if (candidates.Count == 0)
                {
                    log.LogWarning(
                        "AI gateway found no eligible credential for provider {Provider}, capability {Capability}, model {Model}. ProviderEnabled {ProviderEnabled}, circuitOpen {CircuitOpen}; feature {Feature}; correlation {CorrelationId}",
                        provider, request.RequiredCapability, model, credentials.ProviderEnabled(provider),
                        credentials.IsCircuitOpen(provider), request.Feature, correlationId);
                    continue;
                }
                var attempts = 0;

                foreach (var credential in candidates)
                {
                    if (attempts >= maxAttempts) break;
                    if (paidBudgetBlocked && credential.BillingMode == AiBillingMode.Paid) continue;
                    if (!credentials.IsCredentialStillEligible(credential, request.RequiredCapability, model, allowPaid))
                        continue;
                    attempts++;

                    var estimatedCost = credential.EstimateCost(request);
                    if (credential.BillingMode == AiBillingMode.Paid && (!credential.CanEstimatePaidCost(request) || estimatedCost <= 0))
                    {
                        log.LogWarning("AI gateway skipped paid credential {CredentialId}; no positive cost estimate is available",
                            credential.Id);
                        continue;
                    }

                    AiBudgetReservation reservation;
                    try
                    {
                        reservation = await usageLedger.ReserveAsync(request, credential, model, estimatedCost, cancellationToken);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        log.LogError("AI gateway usage reservation failed; provider call blocked. Feature {Feature} correlation {CorrelationId}",
                            request.Feature, correlationId);
                        return null;
                    }

                    if (!reservation.Allowed)
                    {
                        log.LogWarning("AI gateway budget blocked feature {Feature}: {Reason}; global MTD ₹{GlobalMtd}; tenant MTD ₹{TenantMtd}; correlation {CorrelationId}",
                            request.Feature, reservation.BlockReason, reservation.GlobalMonthToDateInr,
                            reservation.TenantMonthToDateInr, correlationId);
                        if (reservation.BlockReason is "monthly_budget_exceeded" or "paid_provider_disabled")
                        {
                            paidBudgetBlocked = true;
                            break;
                        }
                        return null;
                    }

                    if (reservation.WarningThresholdReached)
                        log.LogWarning("AI gateway budget warning for feature {Feature}; global MTD ₹{GlobalMtd}; tenant MTD ₹{TenantMtd}; correlation {CorrelationId}",
                            request.Feature, reservation.GlobalMonthToDateInr, reservation.TenantMonthToDateInr, correlationId);

                    var stopwatch = Stopwatch.StartNew();
                    var completed = false;
                    var retryCount = 0;
                    try
                    {
                        var timeoutSeconds = Math.Clamp(configuration.GetValue("AiGateway:RequestTimeoutSeconds", 100), 3, 180);
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                        var response = await adapter.ExecuteAsync(request, credential, model, timeout.Token);

                        // The provider receives the response schema (Gemini) or JSON mode (Groq).
                        // Do not add a second gateway-side schema gate: the legacy extraction path
                        // accepted provider JSON and let the feature parser handle partial fields.
                        if (request.ResponseSchema is not null && response.StructuredContent is null)
                            throw new AiProviderException(AiFailureCategory.InvalidStructuredOutput,
                                "The provider returned no structured response.");

                        stopwatch.Stop();
                        var actualCost = credential.ActualCost(response.InputTokens, response.OutputTokens, request);
                        await CompleteUsageAsync(request, reservation.Id, "Succeeded", response.InputTokens,
                            response.OutputTokens, actualCost, stopwatch, null, retryCount, CancellationToken.None);
                        completed = true;
                        credentials.MarkProviderSuccess(provider);
                        log.LogInformation("AI gateway completed feature {Feature} with provider {Provider}, model {Model}, credential {CredentialId}, quota bucket {QuotaBucket}, latency {LatencyMs}ms, correlation {CorrelationId}",
                            request.Feature, provider, model, credential.Id, credential.QuotaBucketId,
                            stopwatch.ElapsedMilliseconds, correlationId);
                        return new AiGatewayResponse(response.StructuredContent, response.Text, provider.ToString(),
                            model, credential.Id, response.InputTokens, response.OutputTokens, correlationId);
                    }
                    catch (AiProviderException e)
                    {
                        stopwatch.Stop();
                        log.LogWarning(
                            "AI gateway provider attempt failed. Feature {Feature}, provider {Provider}, model {Model}, credential {CredentialId}, category {FailureCategory}, HTTP {StatusCode}, retry-after-ms {RetryAfterMs}, correlation {CorrelationId}",
                            request.Feature, provider, model, credential.Id, e.Category,
                            e.StatusCode is null ? null : (int)e.StatusCode.Value,
                            e.RetryAfter is null ? null : (long?)Math.Max(0, e.RetryAfter.Value.TotalMilliseconds),
                            correlationId);
                        await CompleteUsageAsync(request, reservation.Id, "Failed", null, null, null,
                            stopwatch, e.Category.ToString(), retryCount, CancellationToken.None);
                        completed = true;
                        HandleFailure(provider, credential, e);
                        if (e.Category == AiFailureCategory.ContentSafetyRefusal)
                            throw;
                        if (e.Category is AiFailureCategory.RateLimited or AiFailureCategory.InvalidCredential or
                            AiFailureCategory.PermissionDenied or AiFailureCategory.BillingDisabled or
                            AiFailureCategory.UnsupportedModel or AiFailureCategory.InvalidRequest or
                            AiFailureCategory.InvalidStructuredOutput)
                            continue;
                        if (e.Category is AiFailureCategory.ProviderUnavailable or AiFailureCategory.Timeout or AiFailureCategory.Network)
                        {
                            retryCount++;
                            if (attempts < maxAttempts)
                                await BackoffAsync(e.RetryAfter, attempts, cancellationToken);
                            continue;
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        stopwatch.Stop();
                        await CompleteUsageAsync(request, reservation.Id, "Failed", null, null, null,
                            stopwatch, "Cancelled", retryCount, CancellationToken.None);
                        completed = true;
                        throw;
                    }
                    catch (OperationCanceledException)
                    {
                        stopwatch.Stop();
                        await CompleteUsageAsync(request, reservation.Id, "Failed", null, null, null,
                            stopwatch, AiFailureCategory.Timeout.ToString(), retryCount, CancellationToken.None);
                        completed = true;
                        credentials.MarkProviderFailure(provider);
                        retryCount++;
                        if (attempts < maxAttempts) await BackoffAsync(null, attempts, cancellationToken);
                    }
                    catch (Exception e) when (e is HttpRequestException or JsonException or InvalidOperationException)
                    {
                        stopwatch.Stop();
                        await CompleteUsageAsync(request, reservation.Id, "Failed", null, null, null,
                            stopwatch, AiFailureCategory.Network.ToString(), retryCount, CancellationToken.None);
                        completed = true;
                        credentials.MarkProviderFailure(provider);
                        log.LogWarning("AI gateway adapter failed for {Provider} feature {Feature} with {ErrorType}; correlation {CorrelationId}",
                            provider, request.Feature, e.GetType().Name, correlationId);
                        retryCount++;
                        if (attempts < maxAttempts) await BackoffAsync(null, attempts, cancellationToken);
                    }
                    finally
                    {
                        if (!completed)
                            await CompleteUsageAsync(request, reservation.Id, "Failed", null, null, null,
                                stopwatch, "UnknownFailure", retryCount, CancellationToken.None);
                    }
                }
            }
        }

        log.LogWarning("AI gateway exhausted configured providers for feature {Feature}; correlation {CorrelationId}",
            request.Feature, correlationId);
        return null;
    }

    private void HandleFailure(AiProviderKind provider, AiProviderCredential credential, AiProviderException error)
    {
        switch (error.Category)
        {
            case AiFailureCategory.RateLimited:
                credentials.MarkRateLimited(credential, error.RetryAfter);
                break;
            case AiFailureCategory.InvalidCredential:
                credentials.MarkCredentialInvalid(credential);
                break;
            case AiFailureCategory.PermissionDenied:
            case AiFailureCategory.BillingDisabled:
                credentials.MarkRateLimited(credential, TimeSpan.FromHours(1));
                break;
            case AiFailureCategory.ProviderUnavailable:
            case AiFailureCategory.Timeout:
            case AiFailureCategory.Network:
                credentials.MarkProviderFailure(provider);
                break;
        }
    }

    private IReadOnlyList<string> ResolveModels(AiGatewayRequest request, AiProviderKind provider)
    {
        if (request.Models.TryGetValue(provider, out var configured) && configured.Count > 0)
            return configured.Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToArray();

        if (provider == AiProviderKind.Gemini)
        {
            var primary = request.Feature == "VoiceIntent"
                ? configuration["Gemini:VoiceIntentModel"] ?? "gemini-3.5-flash"
                : configuration["Gemini:Model"] ?? "gemini-3.5-flash";
            var values = new List<string> { primary };
            if (request.Feature == "InvoiceExtraction")
            {
                values.Add(configuration["Gemini:FallbackModel"] ?? "gemini-flash-latest");
                values.Add(configuration["Gemini:LastResortModel"] ?? "gemini-2.5-flash-lite");
            }
            else if (request.Feature == "CatalogueExtraction")
                values.Add(configuration["Gemini:FallbackModel"] ?? "gemini-flash-latest");
            return values.Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToArray();
        }

        var groqModel = request.Task == AiTaskKind.AudioTranscription
            ? configuration["Groq:WhisperModel"] ?? "whisper-large-v3-turbo"
            : request.Feature == "VoiceIntent"
                ? configuration["Groq:VoiceIntentModel"] ?? "qwen/qwen3.8-27b"
                : configuration["Groq:Model"] ?? "qwen/qwen3.8-27b";
        return [groqModel];
    }

    private async Task CompleteUsageAsync(
        AiGatewayRequest request, Guid reservationId, string outcome, int? inputTokens, int? outputTokens,
        decimal? actualCost, Stopwatch timer, string? errorCategory, int retryCount, CancellationToken ct)
    {
        try
        {
            await usageLedger.CompleteAsync(request, reservationId, outcome, inputTokens, outputTokens,
                actualCost, (int)Math.Clamp(timer.ElapsedMilliseconds, 0, int.MaxValue),
                errorCategory, retryCount, ct);
        }
        catch (Exception e)
        {
            // A still-reserved row continues to count against the budget; never hide a provider response.
            log.LogError("AI gateway could not finalize usage event {ReservationId} ({ErrorType})",
                reservationId, e.GetType().Name);
        }
    }

    private static async Task BackoffAsync(TimeSpan? retryAfter, int attempt, CancellationToken ct)
    {
        var baseDelay = retryAfter ?? TimeSpan.FromMilliseconds(Math.Min(500 * Math.Pow(2, attempt), 4000));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 251));
        var delay = TimeSpan.FromMilliseconds(Math.Clamp(baseDelay.TotalMilliseconds + jitter.TotalMilliseconds, 100, 10_000));
        await Task.Delay(delay, ct);
    }
}

/// <summary>Small schema validator for the subset emitted by the existing Gemini contracts.</summary>
public static class AiStructuredOutputValidator
{
    public static bool IsValid(JsonNode value, JsonObject schema) => Validate(value, schema);

    private static bool Validate(JsonNode? value, JsonObject schema)
    {
        var type = schema["type"]?.GetValue<string>()?.ToUpperInvariant();
        if (value is null) return schema["nullable"]?.GetValue<bool>() == true;
        switch (type)
        {
            case "OBJECT":
                if (value is not JsonObject obj) return false;
                if (schema["required"] is JsonArray required &&
                    required.Any(x => x is not JsonValue j || !j.TryGetValue<string>(out var key) || !obj.ContainsKey(key)))
                    return false;
                if (schema["properties"] is JsonObject properties)
                    foreach (var item in properties)
                        if (obj.TryGetPropertyValue(item.Key, out var child) &&
                            item.Value is JsonObject childSchema && !Validate(child, childSchema))
                            return false;
                return true;
            case "ARRAY":
                if (value is not JsonArray array) return false;
                return schema["items"] is not JsonObject itemSchema ||
                    array.All(x => Validate(x, itemSchema));
            case "STRING":
                if (value is not JsonValue stringValue || !stringValue.TryGetValue<string>(out var text)) return false;
                return schema["enum"] is not JsonArray choices ||
                    choices.Any(x => x is JsonValue choice && choice.TryGetValue<string>(out var allowed) && allowed == text);
            case "NUMBER":
                return value is JsonValue number &&
                    (number.TryGetValue<decimal>(out _) || number.TryGetValue<double>(out _) ||
                     number.TryGetValue<long>(out _) || number.TryGetValue<int>(out _) ||
                     (number.TryGetValue<string>(out var numericText) &&
                      decimal.TryParse(numericText, NumberStyles.Number, CultureInfo.InvariantCulture, out _)));
            case "INTEGER":
                return value is JsonValue integer &&
                    (integer.TryGetValue<long>(out _) || integer.TryGetValue<int>(out _));
            case "BOOLEAN":
                return value is JsonValue boolean && boolean.TryGetValue<bool>(out _);
            default:
                return true;
        }
    }
}
