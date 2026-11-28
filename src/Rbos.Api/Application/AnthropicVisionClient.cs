using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rbos.Api.Application;

public sealed record AnthropicVisionUsage(int InputTokens, int OutputTokens);
public sealed record AnthropicVisionResult(JsonNode Extraction, AnthropicVisionUsage Usage, string Model);

/// <summary>
/// Direct, server-side Anthropic Messages API adapter for handwritten sales-bill extraction.
/// It does not log credentials, document contents, provider bodies or extracted values.
/// </summary>
public sealed class AnthropicVisionClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<AnthropicVisionClient> logger)
{
    public const string DefaultModel = "claude-haiku-5-5";
    const string MessagesEndpoint = "https://api.anthropic.com/v1/messages";
    const string ApiVersion = "2023-06-01";
    const string ToolName = "extract_sales_invoice";

    public bool Available => !string.IsNullOrWhiteSpace(configuration["Anthropic:ApiKey"]);

    public async Task<AnthropicVisionResult?> ExtractAsync(
        string mime,
        byte[] document,
        string prompt,
        JsonObject responseSchema,
        CancellationToken cancellationToken)
    {
        var apiKey = configuration["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Anthropic sales-invoice extraction is not configured.");

        if (document.Length == 0)
            throw new ArgumentException("The source document is empty.", nameof(document));

        if (mime is not ("image/jpeg" or "image/png" or "image/webp" or "application/pdf"))
            throw new ArgumentOutOfRangeException(nameof(mime), "Unsupported sales-invoice document type.");

        var model = configuration["Anthropic:SalesInvoiceModel"];
        if (string.IsNullOrWhiteSpace(model)) model = DefaultModel;

        var source = new JsonObject
        {
            ["type"] = "base64",
            ["media_type"] = mime,
            ["data"] = Convert.ToBase64String(document)
        };
        var documentBlock = new JsonObject
        {
            ["type"] = mime == "application/pdf" ? "document" : "image",
            ["source"] = source
        };
        var content = new JsonArray
        {
            documentBlock,
            new JsonObject { ["type"] = "text", ["text"] = prompt }
        };
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = Math.Clamp(configuration.GetValue("Anthropic:SalesInvoiceMaxOutputTokens", 4096), 1024, 8192),
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = ToolName,
                    ["description"] = "Transcribe the source sales bill into the supplied versioned extraction schema. Preserve uncertain and absent fields without guessing.",
                    ["input_schema"] = responseSchema.DeepClone()
                }
            },
            ["tool_choice"] = new JsonObject { ["type"] = "tool", ["name"] = ToolName },
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = content }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, MessagesEndpoint);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Do not read/log the body: provider error payloads can echo request details.
            logger.LogWarning("Anthropic sales-invoice request failed. HTTP {StatusCode}, model {Model}",
                (int)response.StatusCode, model);
            throw new HttpRequestException(
                $"Anthropic Messages API returned HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var payload = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        var root = payload.RootElement;
        if (!root.TryGetProperty("content", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
            throw new JsonException("Anthropic response did not contain content blocks.");

        JsonNode? extraction = null;
        foreach (var block in blocks.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) &&
                type.GetString() == "tool_use" &&
                block.TryGetProperty("name", out var name) &&
                name.GetString() == ToolName &&
                block.TryGetProperty("input", out var input))
            {
                extraction = JsonNode.Parse(input.GetRawText());
                break;
            }
        }

        if (extraction is not JsonObject)
            throw new JsonException("Anthropic response did not contain the required extraction tool result.");

        var usage = root.TryGetProperty("usage", out var usageElement)
            ? new AnthropicVisionUsage(
                ReadNonNegativeInt(usageElement, "input_tokens"),
                ReadNonNegativeInt(usageElement, "output_tokens"))
            : new AnthropicVisionUsage(0, 0);

        return new AnthropicVisionResult(extraction, usage, model);
    }

    static int ReadNonNegativeInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.TryGetInt32(out var number) && number >= 0 ? number : 0;
}
