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
        Assert.True(Guid.TryParse(root.GetProperty("sourceDocumentId").GetString(), out var sourceDocumentId));
        var retained = await client.GetJson("/documents/" + sourceDocumentId);
        var document = retained.GetProperty("document");
        Assert.Equal(sourceDocumentId, document.GetProperty("id").GetGuid());
        Assert.Equal("Sale", document.GetProperty("direction").GetString());
        Assert.Equal("HandwrittenBill", document.GetProperty("sourceKind").GetString());
        Assert.Equal("NeedsReview", document.GetProperty("status").GetString());
        Assert.Single(retained.GetProperty("pages").EnumerateArray());
        var retainedPage = await client.Send(HttpMethod.Get, "/documents/" + sourceDocumentId + "/pages/1");
        Assert.Equal(HttpStatusCode.OK, retainedPage.StatusCode);
        Assert.Equal("image/jpeg", retainedPage.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Jpeg, await retainedPage.Content.ReadAsByteArrayAsync());

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
        var sourceDocumentId = body.RootElement.GetProperty("sourceDocumentId").GetGuid();
        Assert.Equal("NeedsReview", (await client.GetJson("/documents/" + sourceDocumentId))
            .GetProperty("document").GetProperty("status").GetString());

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
    public async Task Original_sales_bill_is_tenant_private_and_links_atomically_to_normal_sale()
    {
        var owner = new Client(factory);
        await owner.SignUpAndBootstrap();

        var extraction = await owner.SendBytes(
            "/sales/invoice-image-extract", Jpeg, "image/jpeg", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, extraction.StatusCode);
        using var extractionBody = JsonDocument.Parse(await extraction.Content.ReadAsStringAsync());
        var sourceDocumentId = extractionBody.RootElement.GetProperty("sourceDocumentId").GetGuid();

        var otherTenant = new Client(factory);
        await otherTenant.SignUpAndBootstrap();
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherTenant.Send(HttpMethod.Get, "/documents/" + sourceDocumentId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherTenant.Send(HttpMethod.Get, "/documents/" + sourceDocumentId + "/pages/1")).StatusCode);

        var crossTenantSale = await otherTenant.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null,
            locationId = await otherTenant.Location(),
            saleDate = DateOnly.FromDateTime(DateTime.UtcNow),
            saleType = "Cash",
            lines = new[] { new { productId = (Guid?)null, description = "Unlisted device", quantity = 1m, unitPrice = 50m, discount = 0m } },
            sourceDocumentId
        }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Conflict, crossTenantSale.StatusCode);
        Assert.Empty((await otherTenant.GetJson("/sales")).EnumerateArray());

        var customerResponse = await owner.Send(HttpMethod.Post, "/customers",
            new { name = "Evidence Customer", phone = "9876543210" });
        Assert.Equal(HttpStatusCode.OK, customerResponse.StatusCode);
        var customerId = (await customerResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();
        var saleRequest = new
        {
            customerId,
            requireCustomerDetails = true,
            locationId = await owner.Location(),
            saleDate = DateOnly.FromDateTime(DateTime.UtcNow),
            saleType = "Cash",
            lines = new[] { new { productId = (Guid?)null, description = "Reviewed unlisted device", quantity = 1m, unitPrice = 50m, discount = 0m } },
            sourceDocumentId
        };
        var key = Guid.NewGuid().ToString("N");
        var posted = await owner.Send(HttpMethod.Post, "/sales", saleRequest, key);
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        var saleId = (await posted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var linkedDocument = await owner.GetJson("/documents/" + sourceDocumentId);
        Assert.Equal("Posted", linkedDocument.GetProperty("document").GetProperty("status").GetString());
        Assert.Contains(linkedDocument.GetProperty("statusHistory").EnumerateArray(),
            item => item.GetProperty("status").GetString() == "Posted");
        Assert.Single((await owner.GetJson("/sales")).EnumerateArray());

        var replay = await owner.Send(HttpMethod.Post, "/sales", saleRequest, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True((await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replayed").GetBoolean());
        Assert.Single((await owner.GetJson("/sales")).EnumerateArray());
        Assert.Equal("Posted",
            (await owner.GetJson("/documents/" + sourceDocumentId)).GetProperty("document").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.Send(HttpMethod.Post, "/sales/invoice-image-extract/" + sourceDocumentId + "/discard", new { })).StatusCode);
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
