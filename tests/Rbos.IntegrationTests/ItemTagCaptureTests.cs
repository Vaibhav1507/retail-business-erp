using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public sealed class ItemTagCaptureTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    static string Key() => Guid.NewGuid().ToString("N");
    static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xD9];

    [Fact]
    public async Task Scan_returns_invoice_fields_immediately_without_creating_a_capture_or_storing_the_photo()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();
        var productId = await client.AddProduct("RBOS Tag Scan Television");
        const string code = "4006381333931";

        var link = await client.Send(HttpMethod.Post, "/products/" + productId + "/barcodes",
            new { code, symbology = "EAN_13", packQuantity = 1 });
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);

        var response = await client.SendBytes(
            "/sales/item-tag-scan", Jpeg, "image/jpeg", Key(), code, "EAN_13");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("ReadyForReview", body.GetProperty("status").GetString());
        Assert.Equal(code, body.GetProperty("extraction").GetProperty("barcode").GetString());
        Assert.Equal(productId, body.GetProperty("suggestedProduct").GetProperty("id").GetGuid());
        Assert.False(body.TryGetProperty("id", out _));
        Assert.False(body.TryGetProperty("captureId", out _));

        // The old evidence-upload API is deliberately no longer exposed. A tag image
        // exists only in the scan request and the browser's temporary preview.
        var oldUpload = await client.Send(HttpMethod.Post, "/sales/item-tag-captures", new { });
        Assert.Equal(HttpStatusCode.NotFound, oldUpload.StatusCode);
    }

    [Fact]
    public async Task Scan_does_not_replace_an_invalid_locally_decoded_barcode_with_an_ai_guess()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();
        const string invalidEan13 = "4006381333932";

        var response = await client.SendBytes(
            "/sales/item-tag-scan", Jpeg, "image/jpeg", Key(), invalidEan13, "EAN_13");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("extraction").GetProperty("barcode").ValueKind == JsonValueKind.Null);
        Assert.Contains(body.GetProperty("warnings").EnumerateArray(),
            warning => warning.GetString()!.Contains("not replaced with an AI guess", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scan_without_barcode_or_provider_returns_a_manual_review_result_not_a_queued_job()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();

        var response = await client.SendBytes(
            "/sales/item-tag-scan", Jpeg, "image/jpeg", Key());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("NeedsAttention", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("extraction").ValueKind == JsonValueKind.Object);
        Assert.False(body.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Scan_rejects_unsupported_type_bad_signature_and_oversized_photo()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();

        var unsupported = await client.SendBytes(
            "/sales/item-tag-scan", Jpeg, "application/octet-stream", Key());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);

        var wrongSignature = await client.SendBytes(
            "/sales/item-tag-scan", [0, 1, 2, 3], "image/jpeg", Key());
        Assert.Equal(HttpStatusCode.BadRequest, wrongSignature.StatusCode);

        var oversized = new byte[8 * 1024 * 1024 + 1];
        oversized[0] = 0xFF; oversized[1] = 0xD8; oversized[2] = 0xFF; oversized[3] = 0xD9;
        var tooLarge = await client.SendBytes(
            "/sales/item-tag-scan", oversized, "image/jpeg", Key());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
    }

    [Fact]
    public async Task Android_asset_links_endpoint_is_public_and_returns_json_array()
    {
        var client = new Client(factory);
        var response = await client.Raw("/.well-known/assetlinks.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(JsonValueKind.Array, body.RootElement.ValueKind);
    }

    [Fact]
    public async Task Scan_route_requires_authentication()
    {
        var client = new Client(factory);
        var response = await client.SendBytes(
            "/sales/item-tag-scan", Jpeg, "image/jpeg", Key());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
