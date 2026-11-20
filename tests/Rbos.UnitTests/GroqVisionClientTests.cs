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
        string? capturedScheme = null;
        string? capturedToken = null;
        string? capturedBody = null;
        var handler = new StubHandler(async request =>
        {
            capturedScheme = request.Headers.Authorization?.Scheme;
            capturedToken = request.Headers.Authorization?.Parameter;
            capturedBody = await request.Content!.ReadAsStringAsync();
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
        Assert.Equal("Bearer", capturedScheme);
        Assert.Equal("test-key", capturedToken);
        Assert.Contains("qwen/qwen3.8-27b", capturedBody);
        Assert.Contains("data:image/png;base64,", capturedBody);
    }

    [Fact]
    public async Task ExtractInvoiceAsync_UsesVisionModelAndReturnsStructuredInvoiceJson()
    {
        string? capturedBody = null;
        var handler = new StubHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"{\"supplierName\":\"Acme\",\"lines\":[{\"description\":\"Cable\",\"quantity\":2,\"unitCost\":10,\"amount\":20}]}"}}]}""",
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

        var result = await client.ExtractInvoiceAsync([("image/png", Convert.ToBase64String([1, 2, 3]))], "Extract invoice", CancellationToken.None);

        Assert.Equal("Acme", result?["supplierName"]?.GetValue<string>());
        Assert.Equal("Cable", result?["lines"]?[0]?["description"]?.GetValue<string>());
        Assert.Contains("qwen/qwen3.8-27b", capturedBody);
        Assert.Contains("data:image/png;base64,", capturedBody);
    }

    [Fact]
    public async Task ExtractInvoiceAsync_BinaryPages_EncodesOnlyWhenBuildingProviderPayload()
    {
        string? capturedBody = null;
        var handler = new StubHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"content":"{\"supplierName\":\"Acme\",\"lines\":[{\"description\":\"USB cable\",\"quantity\":1,\"unitCost\":10,\"amount\":10}]}"}}]}""",
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

        var result = await client.ExtractInvoiceAsync(
            [("image/png", new byte[] { 1, 2, 3 })], "Extract invoice", CancellationToken.None);

        Assert.Equal("Acme", result?["supplierName"]?.GetValue<string>());
        Assert.Contains("data:image/png;base64,AQID", capturedBody);
    }

    [Fact]
    public async Task ExtractInvoiceAsync_BatchesAtMostThreeImagesAndMergesLines()
    {
        var calls = 0;
        var handler = new StubHandler(async request =>
        {
            calls++;
            var body = await request.Content!.ReadAsStringAsync();
            var imageCount = System.Text.RegularExpressions.Regex.Matches(body, "\\\"type\\\":\\\"image_url\\\"").Count;
            Assert.InRange(imageCount, 1, 3);
            var line = calls == 1 ? "Cable A" : "Cable B";
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content = System.Text.Json.JsonSerializer.Serialize(new { supplierName = "Acme", lines = new[] { new { description = line, quantity = 1, unitCost = 10, amount = 10 } } }) } } }
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Groq:ApiKey"] = "test-key" }).Build();
        using var http = new HttpClient(handler);
        var client = new GroqVisionClient(http, config, NullLogger<GroqVisionClient>.Instance);

        var pages = Enumerable.Range(0, 4).Select(_ => ("image/png", Convert.ToBase64String([1, 2, 3]))).ToArray();
        var result = await client.ExtractInvoiceAsync(pages, "Extract invoice", CancellationToken.None);

        Assert.Equal(2, calls);
        Assert.Equal(2, result?["lines"]?.AsArray().Count);
        Assert.Equal("Cable A", result?["lines"]?[0]?["description"]?.GetValue<string>());
        Assert.Equal("Cable B", result?["lines"]?[1]?["description"]?.GetValue<string>());
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
