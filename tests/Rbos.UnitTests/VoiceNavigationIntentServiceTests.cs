using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public sealed class VoiceNavigationIntentServiceTests
{
    [Fact]
    public async Task ClassifyAsync_UsesGeminiWhenItReturnsAnAllowlistedHighConfidenceDestination()
    {
        var gemini = new StubHandler(_ => Task.FromResult(JsonResponse(
            """{"candidates":[{"content":{"parts":[{"text":"{\"intent\":\"navigate\",\"destinationId\":\"stock.current\",\"confidence\":0.96,\"clarificationCandidates\":[],\"reason\":null}"}]}}]}""")));
        var groq = new StubHandler(_ => throw new InvalidOperationException("Groq must not be called after valid Gemini output."));
        var config = Config(geminiEnabled: true, groqEnabled: true);
        using var geminiHttp = new HttpClient(gemini);
        using var groqHttp = new HttpClient(groq);
        var groqClient = new GroqTextIntentClient(groqHttp, config, NullLogger<GroqTextIntentClient>.Instance);
        var service = new VoiceNavigationIntentService(geminiHttp, config, NullLogger<VoiceNavigationIntentService>.Instance, groqClient);

        var result = await service.ClassifyAsync(new VoiceIntentRequest("open stock", "home"), CancellationToken.None);

        Assert.Equal("navigate", result.Kind);
        Assert.Equal("stock.current", result.TargetId);
        Assert.Equal("gemini", result.Provider);
        Assert.Equal(0.96, result.Confidence);
    }

    [Fact]
    public async Task ClassifyAsync_FallsBackToGroqWhenGeminiReturnsAnUnknownDestination()
    {
        var gemini = new StubHandler(_ => Task.FromResult(JsonResponse(
            """{"candidates":[{"content":{"parts":[{"text":"{\"intent\":\"navigate\",\"destinationId\":\"https://evil.example\",\"confidence\":0.99,\"clarificationCandidates\":[],\"reason\":null}"}]}}]}""")));
        var groq = new StubHandler(_ => Task.FromResult(JsonResponse(
            """{"choices":[{"message":{"content":"{\"intent\":\"navigate\",\"destinationId\":\"buy.receive\",\"confidence\":0.91,\"clarificationCandidates\":[],\"reason\":null}"}}]}""")));
        var config = Config(geminiEnabled: true, groqEnabled: true);
        using var geminiHttp = new HttpClient(gemini);
        using var groqHttp = new HttpClient(groq);
        var groqClient = new GroqTextIntentClient(groqHttp, config, NullLogger<GroqTextIntentClient>.Instance);
        var service = new VoiceNavigationIntentService(geminiHttp, config, NullLogger<VoiceNavigationIntentService>.Instance, groqClient);

        var result = await service.ClassifyAsync(new VoiceIntentRequest("add purchase", "home"), CancellationToken.None);

        Assert.Equal("navigate", result.Kind);
        Assert.Equal("buy.receive", result.TargetId);
        Assert.Equal("groq-qwen", result.Provider);
    }

    [Fact]
    public async Task ClassifyAsync_DoesNotEnableProvidersWithoutExplicitFeatureFlags()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("No provider should be called."));
        var config = new ConfigurationBuilder().Build();
        using var http = new HttpClient(handler);
        var groqClient = new GroqTextIntentClient(http, config, NullLogger<GroqTextIntentClient>.Instance);
        var service = new VoiceNavigationIntentService(http, config, NullLogger<VoiceNavigationIntentService>.Instance, groqClient);

        var result = await service.ClassifyAsync(new VoiceIntentRequest("open stock", null), CancellationToken.None);

        Assert.Equal("not-understood", result.Kind);
        Assert.False(service.Available);
    }

    static IConfiguration Config(bool geminiEnabled, bool groqEnabled) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VoiceIntent:Enabled"] = "true",
            ["Gemini:ApiKey"] = "gemini-test",
            ["Gemini:VoiceIntentEnabled"] = geminiEnabled ? "true" : "false",
            ["Groq:ApiKey"] = "groq-test",
            ["Groq:VoiceIntentEnabled"] = groqEnabled ? "true" : "false",
            ["Groq:VoiceIntentModel"] = "qwen/qwen3.8-27b"
        }).Build();

    static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
