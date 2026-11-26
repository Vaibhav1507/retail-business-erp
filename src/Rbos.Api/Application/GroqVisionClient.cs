using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rbos.Api.Application;

/// <summary>
/// Groq's OpenAI-compatible vision endpoint. API keys are read only from server configuration.
/// </summary>
public sealed class GroqVisionClient(HttpClient http, IConfiguration cfg, ILogger<GroqVisionClient> log)
{
    const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
    const string DefaultModel = "qwen/qwen3.8-27b";

    public bool Available => !string.IsNullOrWhiteSpace(cfg["Groq:ApiKey"]);
    public string Model => cfg["Groq:Model"] ?? DefaultModel;

    public async Task<JsonNode?> ExtractImageAsync(string mime, string base64, string prompt, CancellationToken ct)
    {
        if (!Available) return null;
        return await CallAsync([("data:" + mime + ";base64," + base64)], prompt, ct);
    }

    public async Task<JsonNode?> ExtractInvoiceAsync(IReadOnlyList<(string Mime, string Base64)> pages, string prompt, CancellationToken ct)
    {
        if (!Available || pages.Count is 0 or > 6) return null;
        var images = new List<string>();
        foreach (var (mime, base64) in pages)
        {
            ct.ThrowIfCancellationRequested();
            if (mime == "application/pdf")
            {
                byte[] pdf;
                try { pdf = Convert.FromBase64String(base64); }
                catch (FormatException) { return null; }
                var pdfPages = PdfPageReader.Read(pdf);
                if (pdfPages is null || pdfPages.Count is 0 or > 6) return null;
                foreach (var page in pdfPages)
                {
                    var png = PdfPageReader.RenderPng(pdf, page.Page);
                    if (png is null) return null;
                    images.Add("data:image/png;base64," + Convert.ToBase64String(png));
                }
            }
            else
            {
                if (mime is not ("image/jpeg" or "image/png" or "image/webp")) return null;
                images.Add("data:" + mime + ";base64," + base64);
            }
            if (images.Count > 6) return null;
        }

        var batches = images.Chunk(3).ToArray();
        var partials = await Task.WhenAll(batches.Select(batch =>
            CallAsync(batch, prompt + " These are consecutive pages from the same invoice. Extract all line items visible in these pages. The final page may contain the grand total.", ct)));
        return MergeInvoicePageResults(partials);
    }

    public async Task<JsonNode?> ExtractInvoiceAsync(IReadOnlyList<(string Mime, byte[] Bytes)> pages, string prompt, CancellationToken ct)
    {
        if (!Available || pages.Count is 0 or > 6) return null;
        var images = new List<string>();
        foreach (var (mime, bytes) in pages)
        {
            ct.ThrowIfCancellationRequested();
            if (mime == "application/pdf")
            {
                var pdfPages = PdfPageReader.Read(bytes);
                if (pdfPages is null || pdfPages.Count is 0 or > 6) return null;
                foreach (var page in pdfPages)
                {
                    var png = PdfPageReader.RenderPng(bytes, page.Page);
                    if (png is null) return null;
                    images.Add("data:image/png;base64," + Convert.ToBase64String(png));
                }
            }
            else
            {
                if (mime is not ("image/jpeg" or "image/png" or "image/webp")) return null;
                images.Add("data:" + mime + ";base64," + Convert.ToBase64String(bytes));
            }
            if (images.Count > 6) return null;
        }

        var batches = images.Chunk(3).ToArray();
        var partials = await Task.WhenAll(batches.Select(batch =>
            CallAsync(batch, prompt + " These are consecutive pages from the same invoice. Extract all line items visible in these pages. The final page may contain the grand total.", ct)));
        return MergeInvoicePageResults(partials);
    }

    public static JsonNode? MergeInvoicePageResults(IReadOnlyList<JsonNode?> partials)
    {
        var merged = new JsonObject { ["lines"] = new JsonArray() };
        var identityFields = new[] { "supplierName", "customerName", "customerPhone", "invoiceNumber", "date" };
        var trailingSummaryFields = new[] { "total", "subtotal", "taxAmount" };
        var confidenceFields = new[] { "supplierName", "customerName", "customerPhone", "invoiceNumber", "date", "total", "subtotal", "taxAmount" };
        var mergedConfidence = new JsonObject();
        foreach (var partial in partials)
        {
            if (partial is not JsonObject) return null;
            foreach (var key in identityFields)
            {
                if (HasUsableScalar(merged[key])) continue;
                if (HasUsableScalar(partial[key])) merged[key] = partial[key]!.DeepClone();
            }
            foreach (var key in trailingSummaryFields)
                if (HasUsableScalar(partial[key])) merged[key] = partial[key]!.DeepClone();
            foreach (var key in confidenceFields)
                if (mergedConfidence[key] is null && HasUsableScalar(partial["fieldConfidence"]?[key]))
                    mergedConfidence[key] = partial["fieldConfidence"]![key]!.DeepClone();
            if (partial["lines"] is JsonArray lines)
                foreach (var line in lines)
                    if (line is not null) ((JsonArray)merged["lines"]!).Add(line.DeepClone());
        }
        merged["fieldConfidence"] = mergedConfidence;
        return merged;
    }

    static bool HasUsableScalar(JsonNode? value)
    {
        if (value is null) return false;
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text))
            return !string.IsNullOrWhiteSpace(text);
        return value is JsonValue;
    }

    public async Task<JsonNode?> ExtractPdfAsync(byte[] pdf, string prompt, CancellationToken ct)
    {
        if (!Available) return null;

        var pages = PdfPageReader.Read(pdf);
        if (pages is null || pages.Count == 0) return null;

        var combined = new JsonArray();
        // Groq vision currently accepts at most three images per request for the configured
        // Qwen 3.8 model. Process every page in bounded batches rather than silently truncating.
        foreach (var batch in pages.Chunk(3))
        {
            var images = new List<string>(batch.Length);
            foreach (var page in batch)
            {
                ct.ThrowIfCancellationRequested();
                var png = PdfPageReader.RenderPng(pdf, page.Page);
                if (png is null)
                {
                    log.LogWarning("Could not render PDF page {Page} for Groq vision fallback", page.Page);
                    return null;
                }
                images.Add("data:image/png;base64," + Convert.ToBase64String(png));
            }

            var result = await CallAsync(images, prompt + " These are consecutive pages from the same PDF. Extract only products visible on these pages.", ct);
            if (result?["products"] is not JsonArray products) return null;
            foreach (var product in products)
                if (product is not null) combined.Add(product.DeepClone());
        }

        return new JsonObject { ["products"] = combined };
    }

    static (string? Type, string? Code, string? Parameter) ReadErrorMetadata(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object)
                root = error;

            static string? ReadSafe(JsonElement element, string property)
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    !element.TryGetProperty(property, out var value) ||
                    value.ValueKind != JsonValueKind.String)
                    return null;
                var raw = value.GetString();
                if (string.IsNullOrWhiteSpace(raw)) return null;
                return new string(raw.Take(64)
                    .Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')
                    .ToArray());
            }

            return (ReadSafe(root, "type"), ReadSafe(root, "code"), ReadSafe(root, "param"));
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }

    async Task<JsonNode?> CallAsync(IReadOnlyList<string> images, string prompt, CancellationToken ct)
    {
        var key = cfg["Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return null;

        var content = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = prompt } };
        foreach (var image in images)
        {
            content.Add(new JsonObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JsonObject { ["url"] = image }
            });
        }

        var body = new JsonObject
        {
            ["model"] = Model,
            ["temperature"] = 0,
            ["response_format"] = new JsonObject { ["type"] = "json_object" },
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = content
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // Capture only bounded provider diagnostics. Never log the response body,
            // prompt, uploaded image, authorization header or credential.
            var metadata = ReadErrorMetadata(responseBody);
            log.LogWarning(
                "Groq vision request failed with HTTP {StatusCode}; error type {ErrorType}, code {ErrorCode}, parameter {ErrorParameter}",
                (int)response.StatusCode, metadata.Type, metadata.Code, metadata.Parameter);
            response.EnsureSuccessStatusCode();
        }

        var root = JsonNode.Parse(responseBody);
        var finishReason = root?["choices"]?[0]?["finish_reason"]?.GetValue<string>();
        if (finishReason is "content_filter" or "safety" or "refusal")
            throw new VisionProviderRefusalException("Groq declined the vision extraction request.");
        var text = root?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }
}
