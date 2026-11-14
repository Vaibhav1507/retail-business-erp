using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Rbos.Api.Application;

/// <summary>Text-only Groq chat client for constrained voice-navigation intent fallback.</summary>
public sealed class GroqTextIntentClient(HttpClient http, IConfiguration cfg, ILogger<GroqTextIntentClient> log)
{
    const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";

    public bool Available =>
        string.Equals(cfg["Groq:VoiceIntentEnabled"], "true", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(cfg["Groq:ApiKey"]);

    public async Task<JsonNode?> ClassifyAsync(string systemPrompt, string userInput, CancellationToken ct)
    {
        if (!Available) return null;
        var body = new JsonObject
        {
            ["model"] = cfg["Groq:VoiceIntentModel"] ?? "qwen/qwen3.8-27b",
            ["temperature"] = 0,
            ["max_completion_tokens"] = 250,
            ["reasoning_effort"] = "none",
            ["reasoning_format"] = "hidden",
            ["response_format"] = new JsonObject { ["type"] = "json_object" },
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userInput }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg["Groq:ApiKey"]);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("Groq voice intent request failed with HTTP {StatusCode}", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }
        var root = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        var content = root?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(content) ? null : JsonNode.Parse(content);
    }
}
