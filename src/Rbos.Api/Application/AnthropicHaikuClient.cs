using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rbos.Api.Application;

public sealed record AnthropicSalesInvoiceResult(
    JsonNode Extraction,
    string Model,
    int InputTokens,
    int OutputTokens,
    string? RequestId);

public sealed class AnthropicApiException(
    string message,
    HttpStatusCode? statusCode,
    string? requestId,
    bool isTransient) : HttpRequestException(message, null, statusCode)
{
    public string? RequestId { get; } = requestId;
    public bool IsTransient { get; } = isTransient;
}

/// <summary>
/// Direct Anthropic Messages API client for handwritten sales-bill extraction.
/// It sends the original image/PDF and a constrained JSON schema. OCR text is
/// intentionally not accepted by this client, so independent OCR cannot prime Haiku.
/// </summary>
public sealed class AnthropicHaikuClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<AnthropicHaikuClient> logger)
{
    const string MessagesEndpoint = "https://api.anthropic.com/v1/messages";
    const string DefaultApiVersion = "2023-06-01";

    public string Model =>
        configuration["Anthropic:SalesInvoiceModel"] ??
        configuration["Anthropic:Model"] ??
        "claude-haiku-5-5";

    private string? ConfiguredApiKey =>
        !string.IsNullOrWhiteSpace(configuration["Anthropic:ApiKey"])
            ? configuration["Anthropic:ApiKey"]
            : configuration["ANTHROPIC_API_KEY"];

    public bool Available => !string.IsNullOrWhiteSpace(ConfiguredApiKey);

    public async Task<AnthropicSalesInvoiceResult?> ExtractAsync(
        string mime,
        byte[] originalDocument,
        JsonObject responseSchema,
        string extractionPrompt,
        CancellationToken cancellationToken)
    {
        var apiKey = ConfiguredApiKey;
        if (string.IsNullOrWhiteSpace(apiKey)) return null;
        if (originalDocument.Length is 0 or > 8 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(originalDocument), "The source document must be between 1 byte and 8 MB.");
        if (mime is not ("image/jpeg" or "image/png" or "image/webp" or "application/pdf"))
            throw new ArgumentOutOfRangeException(nameof(mime), "Unsupported Anthropic vision media type.");

        var source = new JsonObject
        {
            ["type"] = "base64",
            ["media_type"] = mime,
            ["data"] = Convert.ToBase64String(originalDocument)
        };
        var sourceBlock = mime == "application/pdf"
            ? new JsonObject { ["type"] = "document", ["source"] = source }
            : new JsonObject { ["type"] = "image", ["source"] = source };

        var content = new JsonArray
        {
            sourceBlock,
            new JsonObject { ["type"] = "text", ["text"] = extractionPrompt }
        };
        var body = new JsonObject
        {
            ["model"] = Model,
            ["max_tokens"] = Math.Clamp(configuration.GetValue("Anthropic:SalesInvoiceMaxOutputTokens", 8192), 1024, 16384),
            ["messages"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["content"] = content
            }),
            ["output_config"] = new JsonObject
            {
                ["format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["schema"] = RemoveUnsupportedSchemaConstraints(responseSchema)
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation(
            "anthropic-version",
            configuration["Anthropic:ApiVersion"] ?? DefaultApiVersion);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var requestId = response.Headers.TryGetValues("request-id", out var requestIds)
            ? requestIds.FirstOrDefault()
            : null;
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var (errorType, errorMessage) = ReadSafeError(responseText);
            var status = response.StatusCode;
            var transient = IsTransientStatus((int)status);
            logger.LogWarning(
                "Anthropic Messages request failed. HTTP {StatusCode}, error type {ErrorType}, request {RequestId}, response bytes {ResponseBytes}, transient {Transient}",
                (int)status, errorType ?? "unknown", requestId, Encoding.UTF8.GetByteCount(responseText), transient);
            var detail = string.IsNullOrWhiteSpace(errorMessage)
                ? $"Anthropic Messages API returned HTTP {(int)status} with an empty or unreadable error body."
                : $"Anthropic Messages API returned HTTP {(int)status}: {errorMessage}";
            throw new AnthropicApiException(detail, status, requestId, transient);
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(responseText) as JsonObject
                ?? throw new JsonException("Anthropic Messages response was not an object.");
        }
        catch (JsonException)
        {
            logger.LogWarning("Anthropic Messages API returned an invalid JSON envelope. Request {RequestId}, response bytes {ResponseBytes}",
                requestId, Encoding.UTF8.GetByteCount(responseText));
            throw new InvalidDataException("Anthropic returned an unreadable response envelope.");
        }

        var stopReason = root["stop_reason"]?.GetValue<string>();
        if (string.Equals(stopReason, "refusal", StringComparison.OrdinalIgnoreCase))
            throw new VisionProviderRefusalException("Anthropic declined the sales-invoice extraction request.");
        if (!string.Equals(stopReason, "end_turn", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Anthropic did not finish the structured extraction response.");

        var text = (root["content"] as JsonArray)?
            .OfType<JsonObject>()
            .Where(block => string.Equals(block["type"]?.GetValue<string>(), "text", StringComparison.Ordinal))
            .Select(block => block["text"]?.GetValue<string>())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("Anthropic returned no structured sales-invoice content.");

        JsonNode extraction;
        try
        {
            extraction = JsonNode.Parse(text)
                ?? throw new JsonException("Structured extraction was null.");
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Anthropic returned invalid structured sales-invoice JSON.");
        }

        var inputTokens = SafeTokenCount(root["usage"]?["input_tokens"]);
        var outputTokens = SafeTokenCount(root["usage"]?["output_tokens"]);
        logger.LogInformation(
            "Anthropic sales-invoice extraction completed. Model {Model}, input tokens {InputTokens}, output tokens {OutputTokens}, request {RequestId}",
            Model, inputTokens, outputTokens, requestId);

        return new AnthropicSalesInvoiceResult(extraction, Model, inputTokens, outputTokens, requestId);
    }

    static JsonObject RemoveUnsupportedSchemaConstraints(JsonObject schema)
    {
        var clone = (JsonObject)schema.DeepClone();
        StripUnsupportedConstraints(clone);
        return clone;
    }

    static void StripUnsupportedConstraints(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in new[] { "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum",
                         "multipleOf", "minLength", "maxLength", "patternProperties", "maxItems", "uniqueItems" })
                obj.Remove(key);
            foreach (var child in obj.ToArray())
                if (child.Value is not null) StripUnsupportedConstraints(child.Value);
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
                if (child is not null) StripUnsupportedConstraints(child);
        }
    }

    static int SafeTokenCount(JsonNode? value)
    {
        try { return Math.Clamp(value?.GetValue<int>() ?? 0, 0, 10_000_000); }
        catch (InvalidOperationException) { return 0; }
        catch (FormatException) { return 0; }
    }

    static (string? Type, string? Message) ReadSafeError(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText)) return (null, null);
        try
        {
            var error = JsonNode.Parse(responseText)?["error"];
            var type = Clip(error?["type"]?.GetValue<string>(), 80);
            var message = Clip(error?["message"]?.GetValue<string>(), 240);
            return (type, message);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return (null, null);
        }
    }

    static string? Clip(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];

    static bool IsTransientStatus(int statusCode) =>
        statusCode is 408 or 425 or 429 or 500 or 502 or 503 or 504 or 529;
}
