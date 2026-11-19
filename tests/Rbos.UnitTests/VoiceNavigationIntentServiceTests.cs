using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public sealed class VoiceNavigationIntentServiceTests
{
    [Fact]
    public async Task ClassifyAsync_UsesGatewayForAllowlistedHighConfidenceGeminiIntent()
    {
        var config = AiGatewayTests.Config(
            ("AiGateway:Enabled", "true"),
            ("AiGateway:Providers:Gemini:Enabled", "true"),
            ("AiGateway:LegacyGeminiBillingMode", "Free"),
            ("Gemini:ApiKey", "gemini-test-secret"),
            ("Gemini:ProjectId", "project-one"),
            ("Gemini:VoiceIntentEnabled", "true"),
            ("VoiceIntent:Enabled", "true"));
        var pool = new AiGatewayCredentialPool(config);
        var adapter = new AiGatewayTests.FakeAdapter(AiProviderKind.Gemini, (_, _, _, _) =>
            Task.FromResult(new AiProviderResponse(Navigate("stock.current", 0.96), null, 20, 10)));
        var gateway = new AiGateway(config, pool, [adapter], new AiGatewayTests.FakeLedger(true), new HttpContextAccessor(), NullLogger<AiGateway>.Instance);
        var service = new VoiceNavigationIntentService(config, NullLogger<VoiceNavigationIntentService>.Instance, gateway);

        var result = await service.ClassifyAsync(Guid.NewGuid(), Guid.NewGuid(),
            new VoiceIntentRequest("open stock", "home"), CancellationToken.None);

        Assert.True(service.Available);
        Assert.Equal("navigate", result.Kind);
        Assert.Equal("stock.current", result.TargetId);
        Assert.Equal("gemini", result.Provider);
        Assert.Equal(0.96, result.Confidence);
        Assert.Equal(1, adapter.Calls);
    }

    [Fact]
    public async Task ClassifyAsync_RejectsUnknownDestinationAndUsesConfiguredGroqFallback()
    {
        var config = AiGatewayTests.Config(
            ("AiGateway:Enabled", "true"),
            ("AiGateway:Providers:Gemini:Enabled", "true"),
            ("AiGateway:Providers:Groq:Enabled", "true"),
            ("AiGateway:LegacyGeminiBillingMode", "Free"),
            ("AiGateway:LegacyGroqBillingMode", "Free"),
            ("Gemini:ApiKey", "gemini-test-secret"),
            ("Gemini:ProjectId", "project-one"),
            ("Gemini:VoiceIntentEnabled", "true"),
            ("Groq:ApiKey", "groq-test-secret"),
            ("Groq:TeamId", "team-one"),
            ("Groq:VoiceIntentEnabled", "true"),
            ("VoiceIntent:Enabled", "true"));
        var pool = new AiGatewayCredentialPool(config);
        var gemini = new AiGatewayTests.FakeAdapter(AiProviderKind.Gemini, (_, _, _, _) =>
            Task.FromResult(new AiProviderResponse(Navigate("https://evil.example", 0.99), null, 20, 10)));
        var groq = new AiGatewayTests.FakeAdapter(AiProviderKind.Groq, (_, _, _, _) =>
            Task.FromResult(new AiProviderResponse(Navigate("buy.receive", 0.91), null, 20, 10)));
        var gateway = new AiGateway(config, pool, [gemini, groq], new AiGatewayTests.FakeLedger(true), new HttpContextAccessor(), NullLogger<AiGateway>.Instance);
        var service = new VoiceNavigationIntentService(config, NullLogger<VoiceNavigationIntentService>.Instance, gateway);

        var result = await service.ClassifyAsync(Guid.NewGuid(), Guid.NewGuid(),
            new VoiceIntentRequest("add purchase", "home"), CancellationToken.None);

        Assert.Equal("navigate", result.Kind);
        Assert.Equal("buy.receive", result.TargetId);
        Assert.Equal("groq-qwen", result.Provider);
        Assert.Equal(1, gemini.Calls);
        Assert.Equal(1, groq.Calls);
    }

    [Fact]
    public void ClassifyAsync_DoesNotEnableProvidersWithoutExplicitFeatureFlags()
    {
        var config = AiGatewayTests.Config();
        var pool = new AiGatewayCredentialPool(config);
        var adapter = new AiGatewayTests.FakeAdapter(AiProviderKind.Gemini, (_, _, _, _) =>
            throw new InvalidOperationException("Provider must not be called."));
        var gateway = new AiGateway(config, pool, [adapter], new AiGatewayTests.FakeLedger(true), new HttpContextAccessor(), NullLogger<AiGateway>.Instance);
        var service = new VoiceNavigationIntentService(config, NullLogger<VoiceNavigationIntentService>.Instance, gateway);

        Assert.False(service.Available);
    }

    private static JsonObject Navigate(string destination, double confidence) => new()
    {
        ["intent"] = "navigate",
        ["destinationId"] = destination,
        ["confidence"] = confidence,
        ["clarificationCandidates"] = new JsonArray(),
        ["reason"] = null
    };
}
