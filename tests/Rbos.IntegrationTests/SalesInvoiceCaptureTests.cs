using System.Net;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public sealed class SalesInvoiceCaptureTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xD9];
    static readonly byte[] Pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n% test PDF header only");

    [Fact]
    public async Task Extract_without_configured_provider_returns_manual_review_draft_and_does_not_post_sale()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();

        var response = await client.SendBytes(
            "/sales/invoice-image-extract", Jpeg, "image/jpeg", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Equal("NeedsAttention", root.GetProperty("status").GetString());
        Assert.Empty(root.GetProperty("extraction").GetProperty("lines").EnumerateArray());
        Assert.NotEmpty(root.GetProperty("warnings").EnumerateArray());
        Assert.False(root.TryGetProperty("saleId", out _));

        var sales = await client.Send(HttpMethod.Get, "/sales", null);
        Assert.Equal(HttpStatusCode.OK, sales.StatusCode);
        using var salesBody = JsonDocument.Parse(await sales.Content.ReadAsStringAsync());
        Assert.Empty(salesBody.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Extract_accepts_pdf_as_an_unposted_review_draft()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();

        var response = await client.SendBytes(
            "/sales/invoice-image-extract", Pdf, "application/pdf", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("NeedsAttention", body.RootElement.GetProperty("status").GetString());
        Assert.Empty(body.RootElement.GetProperty("extraction").GetProperty("lines").EnumerateArray());

        var sales = await client.Send(HttpMethod.Get, "/sales", null);
        using var salesBody = JsonDocument.Parse(await sales.Content.ReadAsStringAsync());
        Assert.Empty(salesBody.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Extract_rejects_unsupported_mime_and_invalid_image_signature()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();

        var unsupported = await client.SendBytes(
            "/sales/invoice-image-extract", Jpeg, "application/octet-stream", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);

        var invalid = await client.SendBytes(
            "/sales/invoice-image-extract", [1, 2, 3, 4], "image/jpeg", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var invalidPdf = await client.SendBytes(
            "/sales/invoice-image-extract", Jpeg, "application/pdf", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidPdf.StatusCode);
    }

    [Fact]
    public async Task Extract_requires_authentication()
    {
        var client = new Client(factory);
        var response = await client.SendBytes(
            "/sales/invoice-image-extract", Jpeg, "image/jpeg", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
