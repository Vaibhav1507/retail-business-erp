using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public sealed class AnthropicHaikuClientTests
{
    [Fact]
    public async Task Sends_original_image_with_messages_schema_and_parses_usage()
    {
        HttpRequestMessage? sent = null;
        var handler = new TestHandler(async request =>
        {
            sent = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"model":"claude-haiku-5-5","stop_reason":"end_turn","content":[{"type":"text","text":"{\u0022invoiceNumber\u0022:\u0022A-001\u0022}"}],"usage":{"input_tokens":321,"output_tokens":45}}""",
                    Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);
        var client = Client(http);

        var result = await client.ExtractAsync(
            "image/jpeg",
            [0xFF, 0xD8, 0xFF, 0xD9],
            new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() },
            "Read the original sales bill. Return the schema fields.",
            CancellationToken.None);

        Assert.NotNull(sent);
        Assert.Equal("https://api.anthropic.com/v1/messages", sent!.RequestUri!.ToString());
        Assert.Equal("test-anthropic-key", sent.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", sent.Headers.GetValues("anthropic-version").Single());
        var json = JsonNode.Parse(await sent.Content!.ReadAsStringAsync())!;
        Assert.Equal("claude-haiku-5-5", json["model"]!.GetValue<string>());
        Assert.Equal("json_schema", json["output_config"]!["format"]!["type"]!.GetValue<string>());
        Assert.Equal("image", json["messages"]![0]!["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("image/jpeg", json["messages"]![0]!["content"]![0]!["source"]!["media_type"]!.GetValue<string>());
        Assert.Equal("Read the original sales bill. Return the schema fields.",
            json["messages"]![0]!["content"]![1]!["text"]!.GetValue<string>());
        Assert.Equal("A-001", result!.Extraction["invoiceNumber"]!.GetValue<string>());
        Assert.Equal(321, result.InputTokens);
        Assert.Equal(45, result.OutputTokens);
    }

    [Fact]
    public async Task Sends_pdf_as_a_document_block_without_image_mime_rewriting()
    {
        HttpRequestMessage? sent = null;
        var handler = new TestHandler(request =>
        {
            sent = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"stop_reason":"end_turn","content":[{"type":"text","text":"{}"}],"usage":{"input_tokens":1,"output_tokens":1}}""",
                    Encoding.UTF8, "application/json")
            });
        });
        using var http = new HttpClient(handler);
        var client = Client(http);

        await client.ExtractAsync("application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7"), new JsonObject(),
            "Read PDF.", CancellationToken.None);

        var json = JsonNode.Parse(await sent!.Content!.ReadAsStringAsync())!;
        Assert.Equal("document", json["messages"]![0]!["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("application/pdf", json["messages"]![0]!["content"]![0]!["source"]!["media_type"]!.GetValue<string>());
    }

    [Fact]
    public async Task Empty_http_400_body_surfaces_safe_request_diagnostics_without_key_or_payload()
    {
        var handler = new TestHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Headers = { { "request-id", "req_test_001" } },
            Content = new StringContent("")
        }));
        using var http = new HttpClient(handler);
        var client = Client(http);

        var error = await Assert.ThrowsAsync<AnthropicApiException>(() =>
            client.ExtractAsync("image/jpeg", [0xFF, 0xD8, 0xFF], new JsonObject(), "prompt with private data", CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal("req_test_001", error.RequestId);
        Assert.False(error.IsTransient);
        Assert.DoesNotContain("test-anthropic-key", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("private data", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_secret_keeps_haiku_provider_unavailable()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var http = new HttpClient(new TestHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var client = new AnthropicHaikuClient(http, configuration, NullLogger<AnthropicHaikuClient>.Instance);
        Assert.False(client.Available);
    }

    static AnthropicHaikuClient Client(HttpClient http)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = "test-anthropic-key",
                ["Anthropic:SalesInvoiceModel"] = "claude-haiku-5-5",
                ["Anthropic:ApiVersion"] = "2023-06-01"
            })
            .Build();
        return new AnthropicHaikuClient(http, configuration, NullLogger<AnthropicHaikuClient>.Instance);
    }

    sealed class TestHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request);
    }
}
