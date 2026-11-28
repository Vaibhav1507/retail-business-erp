using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public sealed class AnthropicVisionClientTests
{
    [Theory]
    [InlineData("image/jpeg", "image")]
    [InlineData("application/pdf", "document")]
    public async Task ExtractAsync_SendsOriginalDocumentAndForcedStructuredTool(
        string mime,
        string expectedBlockType)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "content": [{
                    "type": "tool_use",
                    "name": "extract_sales_invoice",
                    "input": { "invoiceNumber": "00454", "lines": [] }
                  }],
                  "usage": { "input_tokens": 321, "output_tokens": 45 }
                }
                """, Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Anthropic:ApiKey"] = "unit-test-secret",
            ["Anthropic:SalesInvoiceModel"] = AnthropicVisionClient.DefaultModel
        }).Build();
        var client = new AnthropicVisionClient(http, configuration, NullLogger<AnthropicVisionClient>.Instance);
        var schema = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
        var bytes = new byte[] { 1, 2, 3, 4 };

        var result = await client.ExtractAsync(mime, bytes, "Read the bill.", schema, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("claude-haiku-5-5", result.Model);
        Assert.Equal(new AnthropicVisionUsage(321, 45), result.Usage);
        Assert.Equal("00454", result.Extraction["invoiceNumber"]!.GetValue<string>());
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.anthropic.com/v1/messages", handler.RequestUri!.ToString());
        Assert.Equal("unit-test-secret", handler.ApiKey);
        Assert.Equal("2023-06-01", handler.ApiVersion);
        Assert.Equal("application/json", handler.ContentType);
        Assert.Equal(Convert.ToBase64String(bytes), handler.Body!["messages"]![0]!["content"]![0]!["source"]!["data"]!.GetValue<string>());
        Assert.Equal(expectedBlockType, handler.Body["messages"]![0]!["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("tool", handler.Body["tool_choice"]!["type"]!.GetValue<string>());
        Assert.Equal("extract_sales_invoice", handler.Body["tool_choice"]!["name"]!.GetValue<string>());
        Assert.Equal(schema.ToJsonString(), handler.Body["tools"]![0]!["input_schema"]!.ToJsonString());
    }

    [Fact]
    public async Task ExtractAsync_DoesNotExposeProviderErrorBody()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("sensitive provider diagnostic")
        });
        using var http = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Anthropic:ApiKey"] = "unit-test-secret"
        }).Build();
        var client = new AnthropicVisionClient(http, configuration, NullLogger<AnthropicVisionClient>.Instance);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ExtractAsync("image/png", [1], "Read.", new JsonObject(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.DoesNotContain("sensitive provider diagnostic", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractAsync_RejectsMissingCredentialsBeforeNetworkRequest()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("Network must not be called."));
        using var http = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().Build();
        var client = new AnthropicVisionClient(http, configuration, NullLogger<AnthropicVisionClient>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ExtractAsync("image/png", [1], "Read.", new JsonObject(), CancellationToken.None));
    }

    sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? ApiVersion { get; private set; }
        public string? ContentType { get; private set; }
        public JsonObject? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("x-api-key").Single();
            ApiVersion = request.Headers.GetValues("anthropic-version").Single();
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            Body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)) as JsonObject;
            return respond(request);
        }
    }
}
