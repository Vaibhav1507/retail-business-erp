using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rbos.Api.Application;

public sealed class GeminiAiProviderAdapter(
    IHttpClientFactory clients,
    ILogger<GeminiAiProviderAdapter> log) : IAiProviderAdapter
{
    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/";
    public AiProviderKind Provider => AiProviderKind.Gemini;
    public bool Supports(AiCapability capability) => capability != AiCapability.AudioTranscription;

    public async Task<AiProviderResponse> ExecuteAsync(
        AiGatewayRequest request, AiProviderCredential credential, string model, CancellationToken cancellationToken)
    {
        var parts = new JsonArray();
        foreach (var media in request.Media)
        {
            cancellationToken.ThrowIfCancellationRequested();
            parts.Add(new JsonObject
            {
                ["inline_data"] = new JsonObject
                {
                    ["mime_type"] = media.MimeType,
                    ["data"] = Convert.ToBase64String(media.Bytes)
                }
            });
        }

        var userText = request.Prompt;
        if (!string.IsNullOrWhiteSpace(request.InputText))
            userText = string.IsNullOrWhiteSpace(userText)
                ? request.InputText
                : request.Prompt + "\n\n" + request.InputText;
        if (!string.IsNullOrWhiteSpace(userText)) parts.Add(new JsonObject { ["text"] = userText });

        var body = new JsonObject
        {
            ["contents"] = new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = parts }),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0,
                ["maxOutputTokens"] = Math.Clamp(request.MaxOutputTokens, 32, 32_000)
            }
        };
        if (request.ResponseSchema is not null)
        {
            body["generationConfig"]!["responseMimeType"] = "application/json";
            body["generationConfig"]!["responseSchema"] = request.ResponseSchema.DeepClone();
        }
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            body["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = request.SystemPrompt })
            };
        }

        using var message = new HttpRequestMessage(HttpMethod.Post,
            Endpoint + Uri.EscapeDataString(model) + ":generateContent");
        message.Headers.Add("x-goog-api-key", credential.ApiKey);
        message.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var client = clients.CreateClient("AiGateway.Gemini");
        using var response = await client.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("Gemini adapter returned HTTP {StatusCode}", (int)response.StatusCode);
            throw ProviderError(response.StatusCode, response.Headers.RetryAfter, responseBody);
        }

        JsonNode? root;
        try { root = JsonNode.Parse(responseBody); }
        catch (JsonException e) { throw new AiProviderException(AiFailureCategory.InvalidStructuredOutput, "Gemini returned an unreadable response.", response.StatusCode, inner: e); }

        var finishReason = root?["candidates"]?[0]?["finishReason"]?.GetValue<string>();
        if (string.Equals(finishReason, "SAFETY", StringComparison.OrdinalIgnoreCase))
            throw new AiProviderException(AiFailureCategory.ContentSafetyRefusal, "The provider declined this request.");
        var output = root?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(output))
            throw new AiProviderException(AiFailureCategory.InvalidStructuredOutput, "Gemini returned an empty response.");

        JsonNode? structured = null;
        if (request.ResponseSchema is not null)
        {
            try { structured = JsonNode.Parse(output); }
            catch (JsonException e) { throw new AiProviderException(AiFailureCategory.InvalidStructuredOutput, "Gemini returned invalid structured data.", inner: e); }
        }

        return new AiProviderResponse(
            structured,
            request.ResponseSchema is null ? output : null,
            ReadInt(root?["usageMetadata"]?["promptTokenCount"]),
            ReadInt(root?["usageMetadata"]?["candidatesTokenCount"]));
    }

    private static AiProviderException ProviderError(HttpStatusCode status, RetryConditionHeaderValue? retryAfter, string body)
    {
        var retry = ToRetryAfter(retryAfter);
        var lower = body.ToLowerInvariant();
        var category = status switch
        {
            HttpStatusCode.Unauthorized => AiFailureCategory.InvalidCredential,
            HttpStatusCode.PaymentRequired => AiFailureCategory.BillingDisabled,
            HttpStatusCode.Forbidden when lower.Contains("billing") || lower.Contains("payment") => AiFailureCategory.BillingDisabled,
            HttpStatusCode.Forbidden when lower.Contains("api key") || lower.Contains("credential") => AiFailureCategory.InvalidCredential,
            HttpStatusCode.Forbidden => AiFailureCategory.PermissionDenied,
            HttpStatusCode.TooManyRequests => AiFailureCategory.RateLimited,
            HttpStatusCode.RequestTimeout => AiFailureCategory.Timeout,
            HttpStatusCode.BadRequest => AiFailureCategory.InvalidRequest,
            HttpStatusCode.NotFound => AiFailureCategory.UnsupportedModel,
            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => AiFailureCategory.ProviderUnavailable,
            _ => AiFailureCategory.ProviderUnavailable
        };
        return new AiProviderException(category, "Gemini request failed.", status, retry);
    }

    internal static TimeSpan? ToRetryAfter(RetryConditionHeaderValue? value)
    {
        if (value?.Delta is not null) return value.Delta;
        if (value?.Date is not null) return value.Date.Value - DateTimeOffset.UtcNow;
        return null;
    }

    internal static int? ReadInt(JsonNode? node)
    {
        try
        {
            if (node is null) return null;
            var value = node.GetValue<int>();
            return value >= 0 ? value : null;
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException) { return null; }
    }
}

public sealed class GroqAiProviderAdapter(
    IHttpClientFactory clients,
    ILogger<GroqAiProviderAdapter> log) : IAiProviderAdapter
{
    private const string ChatEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    private const string AudioEndpoint = "https://api.groq.com/openai/v1/audio/transcriptions";
    public AiProviderKind Provider => AiProviderKind.Groq;
    public bool Supports(AiCapability capability) => true;

    public async Task<AiProviderResponse> ExecuteAsync(
        AiGatewayRequest request, AiProviderCredential credential, string model, CancellationToken cancellationToken)
    {
        if (request.Task == AiTaskKind.AudioTranscription)
            return await TranscribeAsync(request, credential, model, cancellationToken);

        var images = ExpandMedia(request.Media, cancellationToken);
        if (images.Count == 0)
            return await CompleteChatAsync(request, credential, model, [], cancellationToken);

        JsonNode? merged = null;
        var inputTokens = 0;
        var outputTokens = 0;
        foreach (var batch in images.Chunk(3))
        {
            var response = await CompleteChatAsync(request, credential, model, batch, cancellationToken);
            if (response.StructuredContent is null)
                throw new AiProviderException(AiFailureCategory.InvalidStructuredOutput, "Groq returned invalid structured data.");
            merged = Merge(merged, response.StructuredContent);
            inputTokens += response.InputTokens ?? 0;
            outputTokens += response.OutputTokens ?? 0;
        }
        return new AiProviderResponse(merged, null,
            inputTokens > 0 ? inputTokens : null, outputTokens > 0 ? outputTokens : null);
    }

    private async Task<AiProviderResponse> CompleteChatAsync(
        AiGatewayRequest request, AiProviderCredential credential, string model,
        IReadOnlyList<AiMedia> images, CancellationToken cancellationToken)
    {
        var messages = new JsonArray();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt });

        JsonNode userContent;
        if (images.Count > 0)
        {
            var content = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = BuildUserText(request) }
            };
            foreach (var image in images)
            {
                content.Add(new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject
                    {
                        ["url"] = "data:" + image.MimeType + ";base64," + Convert.ToBase64String(image.Bytes)
                    }
                });
            }
            userContent = content;
        }
        else
            userContent = JsonValue.Create(BuildUserText(request))!;

        messages.Add(new JsonObject { ["role"] = "user", ["content"] = userContent });
        var body = new JsonObject
        {
            ["model"] = model,
            ["temperature"] = 0,
            ["messages"] = messages,
            ["max_completion_tokens"] = Math.Clamp(request.MaxOutputTokens, 32, 32_000)
        };
        if (request.ResponseSchema is not null)
            body["response_format"] = new JsonObject { ["type"] = "json_object" };
        if (request.Task == AiTaskKind.TextClassification)
        {
            body["reasoning_effort"] = "none";
            body["reasoning_format"] = "hidden";
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, ChatEndpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        message.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var client = clients.CreateClient("AiGateway.Groq");
        using var response = await client.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("Groq chat adapter returned HTTP {StatusCode}", (int)response.StatusCode);
            throw ProviderError(response.StatusCode, response.Headers.RetryAfter, responseBody);
        }

        JsonNode? root;
        try { root = JsonNode.Parse(responseBody); }
        catch (JsonException e) { throw new AiProviderException(AiFailureCategory.InvalidStructuredOutput, "Groq returned an unreadable response.", inner: e); }
        var finishReason = root?["choices"]?[0]?["finish_reason"]?.GetValue<string>();
        if (finishReason is "content_filter" or "safety" or "refusal")
            throw new AiProviderException(AiFailureCategory.ContentSafetyRefusal, "The provider declined this request.");
        var output = root?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(output))
            throw new AiProviderException(AiFailureCategory.InvalidStructuredOutput, "Groq returned an empty response.");

        JsonNode? structured = null;
        if (request.ResponseSchema is not null)
        {
            try { structured = JsonNode.Parse(output); }
            catch (JsonException e) { throw new AiProviderException(AiFailureCategory.InvalidStructuredOutput, "Groq returned invalid structured data.", inner: e); }
        }
        return new AiProviderResponse(structured, request.ResponseSchema is null ? output : null,
            GeminiAiProviderAdapter.ReadInt(root?["usage"]?["prompt_tokens"]),
            GeminiAiProviderAdapter.ReadInt(root?["usage"]?["completion_tokens"]));
    }

    private async Task<AiProviderResponse> TranscribeAsync(
        AiGatewayRequest request, AiProviderCredential credential, string model, CancellationToken cancellationToken)
    {
        if (request.Audio is null || request.Audio.Length is <= 44 or > 640_044)
            throw new AiProviderException(AiFailureCategory.InvalidRequest, "Audio is empty or exceeds the hosted transcription limit.");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(request.Audio);
        file.Headers.ContentType = new MediaTypeHeaderValue(request.AudioMimeType);
        form.Add(file, "file", "navigation-command.wav");
        form.Add(new StringContent(model), "model");
        form.Add(new StringContent("json"), "response_format");
        form.Add(new StringContent("0"), "temperature");
        form.Add(new StringContent(request.AudioPrompt ?? request.Prompt), "prompt");

        using var message = new HttpRequestMessage(HttpMethod.Post, AudioEndpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        message.Content = form;
        using var client = clients.CreateClient("AiGateway.Groq");
        using var response = await client.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("Groq audio adapter returned HTTP {StatusCode}", (int)response.StatusCode);
            throw ProviderError(response.StatusCode, response.Headers.RetryAfter, responseBody);
        }
        try
        {
            var json = JsonNode.Parse(responseBody);
            var transcript = json?["text"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(transcript))
                throw new AiProviderException(AiFailureCategory.InvalidStructuredOutput, "Groq returned an empty transcript.");
            return new AiProviderResponse(null, transcript, null, null);
        }
        catch (JsonException e)
        {
            throw new AiProviderException(AiFailureCategory.InvalidStructuredOutput, "Groq returned an unreadable transcript.", inner: e);
        }
    }

    private static string BuildUserText(AiGatewayRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InputText)) return request.Prompt;
        if (string.IsNullOrWhiteSpace(request.Prompt) || request.Prompt == request.SystemPrompt)
            return request.InputText;
        return request.Prompt + "\n\n" + request.InputText;
    }

    private static List<AiMedia> ExpandMedia(IReadOnlyList<AiMedia> media, CancellationToken ct)
    {
        var result = new List<AiMedia>();
        foreach (var item in media)
        {
            ct.ThrowIfCancellationRequested();
            if (item.MimeType == "application/pdf")
            {
                try
                {
                    var pages = PdfPageReader.Read(item.Bytes);
                    if (pages is null || pages.Count == 0 || pages.Count > 30)
                        throw new AiProviderException(AiFailureCategory.InvalidRequest, "The PDF cannot be rendered within the document page limit.");
                    foreach (var page in pages)
                    {
                        ct.ThrowIfCancellationRequested();
                        var png = PdfPageReader.RenderPng(item.Bytes, page.Page);
                        if (png is null) throw new AiProviderException(AiFailureCategory.InvalidRequest, "A PDF page could not be rendered.");
                        result.Add(new AiMedia("image/png", png));
                    }
                }
                catch (AiProviderException) { throw; }
                catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException)
                {
                    throw new AiProviderException(AiFailureCategory.InvalidRequest, "The PDF could not be rendered for Groq vision.", inner: e);
                }
                continue;
            }
            if (item.MimeType is not ("image/jpeg" or "image/png" or "image/webp"))
                throw new AiProviderException(AiFailureCategory.InvalidRequest, "Groq does not support this media format.");
            result.Add(item);
        }
        return result;
    }

    private static JsonNode Merge(JsonNode? current, JsonNode next)
    {
        if (current is not JsonObject target || next is not JsonObject source)
            return next.DeepClone();
        var merged = (JsonObject)target.DeepClone();
        foreach (var property in source)
        {
            if (property.Value is JsonArray array)
            {
                if (merged[property.Key] is not JsonArray existing) merged[property.Key] = new JsonArray();
                foreach (var value in array)
                    if (value is not null) ((JsonArray)merged[property.Key]!).Add(value.DeepClone());
            }
            else if (property.Value is not null)
                merged[property.Key] = property.Value.DeepClone();
        }
        return merged;
    }

    private static AiProviderException ProviderError(HttpStatusCode status, RetryConditionHeaderValue? retryAfter, string body)
    {
        var lower = body.ToLowerInvariant();
        var category = status switch
        {
            HttpStatusCode.Unauthorized => AiFailureCategory.InvalidCredential,
            HttpStatusCode.PaymentRequired => AiFailureCategory.BillingDisabled,
            HttpStatusCode.Forbidden when lower.Contains("billing") || lower.Contains("payment") => AiFailureCategory.BillingDisabled,
            HttpStatusCode.Forbidden when lower.Contains("api key") || lower.Contains("credential") => AiFailureCategory.InvalidCredential,
            HttpStatusCode.Forbidden => AiFailureCategory.PermissionDenied,
            HttpStatusCode.TooManyRequests => AiFailureCategory.RateLimited,
            HttpStatusCode.BadRequest => AiFailureCategory.InvalidRequest,
            HttpStatusCode.NotFound => AiFailureCategory.UnsupportedModel,
            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => AiFailureCategory.ProviderUnavailable,
            _ => AiFailureCategory.ProviderUnavailable
        };
        return new AiProviderException(category, "Groq request failed.", status, GeminiAiProviderAdapter.ToRetryAfter(retryAfter));
    }
}
