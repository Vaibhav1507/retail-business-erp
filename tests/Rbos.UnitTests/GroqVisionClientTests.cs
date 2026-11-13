using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public sealed class GroqVisionClientTests
{
    [Fact]
    public async Task ExtractImageAsync_ReturnsStructuredJson_AndUsesConfiguredModel()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(async request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"{\"products\":[{\"name\":\"USB-C Cable\",\"category\":\"Cables\"}]}"}}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Groq:ApiKey"] = "test-key",
            ["Groq:Model"] = "qwen/qwen3.8-27b"
        }).Build();
        using var http = new HttpClient(handler);
        var client = new GroqVisionClient(http, config, NullLogger<GroqVisionClient>.Instance);

        var result = await client.ExtractImageAsync("image/png", Convert.ToBase64String([1, 2, 3]), "Extract products", CancellationToken.None);

        Assert.Equal("USB-C Cable", result?["products"]?[0]?["name"]?.GetValue<string>());
        Assert.Equal("Bearer", captured?.Headers.Authorization?.Scheme);
        Assert.Equal("test-key", captured?.Headers.Authorization?.Parameter);
        var body = await captured!.Content!.ReadAsStringAsync();
        Assert.Contains("qwen/qwen3.8-27b", body);
        Assert.Contains("data:image/png;base64,", body);
    }

    [Fact]
    public async Task ExtractImageAsync_WithoutApiKey_DoesNotCallProvider()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("HTTP must not be called without a key."));
        var config = new ConfigurationBuilder().Build();
        using var http = new HttpClient(handler);
        var client = new GroqVisionClient(http, config, NullLogger<GroqVisionClient>.Instance);

        var result = await client.ExtractImageAsync("image/png", "AQID", "Extract products", CancellationToken.None);

        Assert.Null(result);
    }

    sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
