using System.Net.Http.Headers;
using System.Text;
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

    public async Task<JsonNode?> ExtractImageAsync(string mime, string base64, string prompt, CancellationToken ct)
    {
        if (!Available) return null;
        return await CallAsync([("data:" + mime + ";base64," + base64)], prompt, ct);
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
            ["model"] = cfg["Groq:Model"] ?? DefaultModel,
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
            // Never log the request, authorization header, uploaded image or response payload.
            log.LogWarning("Groq vision request failed with HTTP {StatusCode}", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        var root = JsonNode.Parse(responseBody);
        var text = root?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }
}
