using System.Net;
using System.Text;
using System.Text.Json;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public class MetaWhatsAppCloudClientTests
{
    sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }

    static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Uploads_pdf_then_sends_document_header_utility_template()
    {
        string? uploadedBody = null;
        string? sentBody = null;
        var call = 0;
        using var http = new HttpClient(new StubHandler(async (request, _) =>
        {
            Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
            if (call++ == 0)
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/v25.0/123456789/media", request.RequestUri!.AbsolutePath);
                Assert.StartsWith("multipart/form-data", request.Content!.Headers.ContentType!.MediaType);
                uploadedBody = await request.Content.ReadAsStringAsync();
                return Json(HttpStatusCode.OK, """{"id":"media-123"}""");
            }

            Assert.Equal("/v25.0/123456789/messages", request.RequestUri!.AbsolutePath);
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            sentBody = await request.Content.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, """{"messages":[{"id":"wamid.test-123"}]}""");
        }));

        var client = new MetaWhatsAppCloudClient(http, "v25.0", "123456789", "test-token", "invoice_utility", "en");
        Assert.Equal("WhatsApp", client.Channel);
        var messageId = await client.SendInvoiceAsync("919876543210", "INV/2026-001", Encoding.ASCII.GetBytes("%PDF-test"), CancellationToken.None);


        Assert.Equal("wamid.test-123", messageId);
        Assert.NotNull(uploadedBody);
        Assert.Contains("messaging_product", uploadedBody);
        Assert.Contains("application/pdf", uploadedBody);
        Assert.Contains("INV2026-001.pdf", uploadedBody);
        Assert.NotNull(sentBody);
        using var payload = JsonDocument.Parse(sentBody);
        var root = payload.RootElement;
        Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
        Assert.Equal("919876543210", root.GetProperty("to").GetString());
        Assert.Equal("invoice_utility", root.GetProperty("template").GetProperty("name").GetString());
        var components = root.GetProperty("template").GetProperty("components");
        Assert.Equal("document", components[0].GetProperty("parameters")[0].GetProperty("type").GetString());
        Assert.Equal("media-123", components[0].GetProperty("parameters")[0].GetProperty("document").GetProperty("id").GetString());
        Assert.Equal("INV/2026-001", components[1].GetProperty("parameters")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Rate_limit_is_retryable_but_uncertain_message_server_error_needs_review()
    {
        using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.TooManyRequests, """{"error":{"code":130429}}"""))));
        var client = new MetaWhatsAppCloudClient(http, "v25.0", "123", "token", "invoice", "en");
        var rateLimit = await Assert.ThrowsAsync<MetaWhatsAppApiException>(() => client.UploadInvoicePdfAsync([1, 2, 3], "INV-1", CancellationToken.None));
        Assert.True(rateLimit.Retryable);
        Assert.False(rateLimit.NeedsReview);

        using var sendHttp = new HttpClient(new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.InternalServerError, """{"error":{"code":1}}"""))));
        var sendClient = new MetaWhatsAppCloudClient(sendHttp, "v25.0", "123", "token", "invoice", "en");
        var uncertain = await Assert.ThrowsAsync<MetaWhatsAppApiException>(() => sendClient.SendInvoiceTemplateAsync("919876543210", "media", "INV-1", CancellationToken.None));
        Assert.False(uncertain.Retryable);
        Assert.True(uncertain.NeedsReview);
    }
}
