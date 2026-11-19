using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public sealed class AiProviderAdapterTests
{
    [Fact]
    public async Task GeminiAdapter_SeparatesSystemInstructionFromTaskAndInput()
    {
        string? capturedKey = null;
        JsonObject? capturedBody = null;
        var factory = new StubFactory(async request =>
        {
            capturedKey = request.Headers.GetValues("x-goog-api-key").Single();
            capturedBody = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!.AsObject();
            return JsonResponse("""{"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"answer"}]}}],"usageMetadata":{"promptTokenCount":12,"candidatesTokenCount":4}}""");
        });
        var adapter = new GeminiAiProviderAdapter(factory, NullLogger<GeminiAiProviderAdapter>.Instance);
        var credential = Credential(AiProviderKind.Gemini, "gemini-secret");
        var request = new AiGatewayRequest
        {
            TenantId = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            Feature = "AdapterTest",
            Task = AiTaskKind.TextGeneration,
            SystemPrompt = "Follow the system rules.",
            Prompt = "Summarize the following input.",
            InputText = "Synthetic sample data",
            ProviderOrder = [AiProviderKind.Gemini]
        };

        var response = await adapter.ExecuteAsync(request, credential, "gemini-test-model", CancellationToken.None);

        Assert.Equal("answer", response.Text);
        Assert.Equal(12, response.InputTokens);
        Assert.Equal(4, response.OutputTokens);
        Assert.Equal("gemini-secret", capturedKey);
        Assert.Equal("Follow the system rules.", capturedBody?["systemInstruction"]?["parts"]?[0]?["text"]?.GetValue<string>());
        var userText = capturedBody?["contents"]?[0]?["parts"]?[0]?["text"]?.GetValue<string>();
        Assert.Contains("Summarize the following input.", userText);
        Assert.Contains("Synthetic sample data", userText);
    }

    [Fact]
    public async Task GeminiAdapter_IgnoresThinkingPartsAndReturnsTheActualAnswer()
    {
        var factory = new StubFactory(_ => Task.FromResult(JsonResponse(
            """{"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"private reasoning","thought":true},{"text":"actual answer"}]}}]}""")));
        var adapter = new GeminiAiProviderAdapter(factory, NullLogger<GeminiAiProviderAdapter>.Instance);
        var request = new AiGatewayRequest
        {
            TenantId = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            Feature = "AdapterTest",
            Task = AiTaskKind.TextGeneration,
            Prompt = "Answer the request.",
            ProviderOrder = [AiProviderKind.Gemini]
        };

        var response = await adapter.ExecuteAsync(request,
            Credential(AiProviderKind.Gemini, "gemini-secret"), "gemini-test-model", CancellationToken.None);

        Assert.Equal("actual answer", response.Text);
    }

    [Fact]
    public async Task GeminiAdapter_ReportsWhenStructuredOutputWasTruncatedByTokenLimit()
    {
        var factory = new StubFactory(_ => Task.FromResult(JsonResponse(
            """{"candidates":[{"finishReason":"MAX_TOKENS","content":{"parts":[{"text":"{\"lines\":["}]}}]}""")));
        var adapter = new GeminiAiProviderAdapter(factory, NullLogger<GeminiAiProviderAdapter>.Instance);
        var request = new AiGatewayRequest
        {
            TenantId = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            Feature = "InvoiceExtraction",
            Task = AiTaskKind.StructuredExtraction,
            Prompt = "Extract invoice.",
            ProviderOrder = [AiProviderKind.Gemini]
        };

        var error = await Assert.ThrowsAsync<AiProviderException>(() =>
            adapter.ExecuteAsync(request, Credential(AiProviderKind.Gemini, "gemini-secret"),
                "gemini-test-model", CancellationToken.None));

        Assert.Equal(AiFailureCategory.InvalidStructuredOutput, error.Category);
        Assert.Contains("token limit", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GeminiAdapter_ClassifiesInvalidApiKeyReturnedAsBadRequest()
    {
        var factory = new StubFactory(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error":{"message":"API key not valid. Please pass a valid API key."}}""",
                Encoding.UTF8, "application/json")
        }));
        var adapter = new GeminiAiProviderAdapter(factory, NullLogger<GeminiAiProviderAdapter>.Instance);
        var request = new AiGatewayRequest
        {
            TenantId = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            Feature = "InvoiceExtraction",
            Task = AiTaskKind.StructuredExtraction,
            Prompt = "Extract invoice.",
            ProviderOrder = [AiProviderKind.Gemini]
        };

        var error = await Assert.ThrowsAsync<AiProviderException>(() =>
            adapter.ExecuteAsync(request, Credential(AiProviderKind.Gemini, "gemini-secret"),
                "gemini-test-model", CancellationToken.None));

        Assert.Equal(AiFailureCategory.InvalidCredential, error.Category);
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task GroqAdapter_TranscribesOnlyWhenAudioTaskIsExplicit()
    {
        string? capturedAuthorization = null;
        string? capturedBody = null;
        var factory = new StubFactory(async request =>
        {
            capturedAuthorization = request.Headers.Authorization?.ToString();
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""{"text":"open stock"}""");
        });
        var adapter = new GroqAiProviderAdapter(factory, NullLogger<GroqAiProviderAdapter>.Instance);
        var credential = Credential(AiProviderKind.Groq, "groq-secret");
        var request = new AiGatewayRequest
        {
            TenantId = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            Feature = "VoiceTranscription",
            Task = AiTaskKind.AudioTranscription,
            Prompt = "Transcribe only.",
            AudioPrompt = "Short navigation command.",
            Audio = new byte[64],
            AudioMimeType = "audio/wav",
            AudioDurationSeconds = 1.0,
            ProviderOrder = [AiProviderKind.Groq]
        };

        var response = await adapter.ExecuteAsync(request, credential, "whisper-test-model", CancellationToken.None);

        Assert.Equal("open stock", response.Text);
        Assert.Equal("Bearer groq-secret", capturedAuthorization);
        Assert.Contains("whisper-test-model", capturedBody);
        Assert.Contains("Short navigation command.", capturedBody);
    }

    private static AiProviderCredential Credential(AiProviderKind provider, string key) =>
        new("test-credential", provider, provider + ":test-bucket", key, AiBillingMode.Free,
            Enum.GetValues<AiCapability>().ToHashSet(), [], 0, 0, 0, 0);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubFactory(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(send), disposeHandler: true);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request);
    }
}
