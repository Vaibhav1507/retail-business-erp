using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public sealed class ItemTagCaptureTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string Key() => Guid.NewGuid().ToString("N");
    static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xD9];

    static async Task<Guid> Upload(Client client, byte[]? bytes = null, string? key = null)
    {
        var response = await client.SendBytes(
            "/sales/item-tag-captures", bytes ?? Jpeg, "image/jpeg", key ?? Key());
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Upload returned {(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    static async Task<HttpResponseMessage> Review(
        Client client, Guid id, Guid productId, string? barcode = null)
        => await client.Send(HttpMethod.Put, "/sales/item-tag-captures/" + id + "/review",
            new
            {
                productId, productName = "RBOS Test Cable", brand = (string?)null, model = (string?)null,
                barcode, barcodeSymbology = barcode is null ? null : "EAN_13",
                serial = (string?)null, imei1 = (string?)null, imei2 = (string?)null,
                mrp = (decimal?)null, hsnCode = (string?)null
            });

    static async Task<(Client Client, Guid LocationId, Guid ProductId)> Stocked(ApiFactory factory)
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();
        var location = await client.Location();
        var product = await client.AddProduct("RBOS Test Cable");
        var supplierResponse = await client.Send(HttpMethod.Post, "/suppliers", new { name = "Capture Test Supplier" });
        Assert.Equal(HttpStatusCode.OK, supplierResponse.StatusCode);
        var supplier = (await supplierResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var purchase = await client.Send(HttpMethod.Post, "/purchases", new
        {
            supplierId = supplier, locationId = location, receiptDate = Today,
            lines = new[] { new { productId = product, quantity = 5m, unitCost = 20m } }
        }, Key());
        Assert.Equal(HttpStatusCode.OK, purchase.StatusCode);
        return (client, location, product);
    }

    [Fact]
    public async Task Upload_is_idempotent_and_image_access_is_private_and_deletable()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();
        var key = Key();

        var first = await client.SendBytes("/sales/item-tag-captures", Jpeg, "image/jpeg", key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var id = firstBody.GetProperty("id").GetGuid();
        Assert.Equal("NeedsAttention", firstBody.GetProperty("status").GetString());

        var replay = await client.SendBytes("/sales/item-tag-captures", Jpeg, "image/jpeg", key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(id, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        var conflict = await client.SendBytes("/sales/item-tag-captures", [0xFF, 0xD8, 0xFF, 0xE0], "image/jpeg", key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var image = await client.Raw("/sales/item-tag-captures/" + id + "/image");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal(Jpeg, await image.Content.ReadAsByteArrayAsync());
        Assert.Contains("private", image.Headers.CacheControl?.ToString());
        Assert.Contains("no-store", image.Headers.CacheControl?.ToString());

        var removed = await client.Send(HttpMethod.Delete, "/sales/item-tag-captures/" + id);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.Raw("/sales/item-tag-captures/" + id)).StatusCode);
    }

    [Fact]
    public async Task Local_EAN13_barcode_hint_is_preserved_and_matched_without_AI()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();
        const string code = "4006381333931";
        var productResponse = await client.Send(HttpMethod.Post, "/products", new
        {
            name = "Barcode Test Product", categoryName = "Accessories", isGeneric = true,
            baseUnit = "pcs", barcode = code
        });
        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);
        var productId = (await productResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var upload = await client.SendBytes(
            "/sales/item-tag-captures", Jpeg, "image/jpeg", Key(), code, "EAN_13");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var body = await upload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ReadyForReview", body.GetProperty("status").GetString());
        Assert.Equal(code, body.GetProperty("extraction").GetProperty("barcode").GetString());
        Assert.Equal(productId, body.GetProperty("suggestedProduct").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Confirmed_barcode_is_learned_for_existing_product_and_reused_on_future_scans()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();
        var productId = await client.AddProduct("Existing LG Television");
        const string code = "4006381333931";

        var captureId = await Upload(client);
        var review = await Review(client, captureId, productId, code);
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        var lookup = await client.GetJson("/products/by-barcode?code=" + code);
        Assert.Equal(productId, lookup.GetProperty("productId").GetGuid());

        var repeated = await Review(client, captureId, productId, code);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var list = await client.GetJson("/products/" + productId + "/barcodes");
        Assert.Single(list.GetProperty("barcodes").EnumerateArray());
    }

    [Fact]
    public async Task Confirmed_barcode_cannot_be_reassigned_from_another_product()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();
        const string code = "4006381333931";
        var original = await client.Send(HttpMethod.Post, "/products", new
        {
            name = "Original barcode owner", categoryName = "Accessories", isGeneric = true,
            baseUnit = "pcs", barcode = code
        });
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var originalId = (await original.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var otherProduct = await client.AddProduct("Different product");

        var captureId = await Upload(client);
        var conflict = await Review(client, captureId, otherProduct, code);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var lookup = await client.GetJson("/products/by-barcode?code=" + code);
        Assert.Equal(originalId, lookup.GetProperty("productId").GetGuid());
    }

    [Fact]
    public async Task Upload_rejects_unsupported_type_bad_signature_and_oversized_photo()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();

        var unsupported = await client.SendBytes("/sales/item-tag-captures", Jpeg, "application/octet-stream", Key());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);

        var wrongSignature = await client.SendBytes("/sales/item-tag-captures", [0, 1, 2, 3], "image/jpeg", Key());
        Assert.Equal(HttpStatusCode.BadRequest, wrongSignature.StatusCode);

        var oversized = new byte[8 * 1024 * 1024 + 1];
        oversized[0] = 0xFF; oversized[1] = 0xD8; oversized[2] = 0xFF; oversized[3] = 0xD9;
        var tooLarge = await client.SendBytes("/sales/item-tag-captures", oversized, "image/jpeg", Key());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
    }

    [Fact]
    public async Task Another_tenant_cannot_read_review_or_delete_a_capture()
    {
        var tenantA = new Client(factory);
        await tenantA.SignUpAndBootstrap();
        var productA = await tenantA.AddProduct("Tenant A Cable");
        var id = await Upload(tenantA);
        await Review(tenantA, id, productA);

        var tenantB = new Client(factory);
        await tenantB.SignUpAndBootstrap();
        Assert.Equal(HttpStatusCode.NotFound, (await tenantB.Raw("/sales/item-tag-captures/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await tenantB.Raw("/sales/item-tag-captures/" + id + "/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await tenantB.Send(HttpMethod.Delete, "/sales/item-tag-captures/" + id)).StatusCode);
        var crossTenantReview = await tenantB.Send(HttpMethod.Put, "/sales/item-tag-captures/" + id + "/review",
            new { productId = await tenantB.AddProduct("Tenant B Cable"), productName = "Tenant B Cable" });
        Assert.Equal(HttpStatusCode.NotFound, crossTenantReview.StatusCode);
    }

    [Fact]
    public async Task Reviewed_photo_is_attached_atomically_to_sale_line_and_remains_retrievable()
    {
        var (client, location, product) = await Stocked(factory);
        var captureId = await Upload(client);
        await Review(client, captureId, product);

        var sale = await client.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null, locationId = location, saleDate = Today, saleType = "Cash",
            amountPaid = (decimal?)null, payMethod = (string?)null,
            lines = new[]
            {
                new { productId = product, quantity = 1m, unitPrice = 100m, discount = 0m,
                    serials = Array.Empty<string>(), captureIds = new[] { captureId } }
            }
        }, Key());
        Assert.Equal(HttpStatusCode.OK, sale.StatusCode);
        var saleId = (await sale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var detail = await client.GetJson("/sales/" + saleId);
        var line = Assert.Single(detail.GetProperty("lines").EnumerateArray());
        Assert.Equal(captureId, Assert.Single(line.GetProperty("captureIds").EnumerateArray()).GetGuid());

        var capture = await client.GetJson("/sales/item-tag-captures/" + captureId);
        Assert.Equal("Attached", capture.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.Raw("/sales/item-tag-captures/" + captureId + "/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.Send(HttpMethod.Delete, "/sales/item-tag-captures/" + captureId)).StatusCode);
    }

    [Fact]
    public async Task Unreviewed_photo_cannot_be_attached_to_a_sale()
    {
        var (client, location, product) = await Stocked(factory);
        var captureId = await Upload(client);
        var sale = await client.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null, locationId = location, saleDate = Today, saleType = "Cash",
            amountPaid = (decimal?)null, payMethod = (string?)null,
            lines = new[]
            {
                new { productId = product, quantity = 1m, unitPrice = 100m, discount = 0m,
                    serials = Array.Empty<string>(), captureIds = new[] { captureId } }
            }
        }, Key());
        Assert.Equal(HttpStatusCode.Conflict, sale.StatusCode);
        Assert.Equal(5m, (await client.GetJson("/stock")).EnumerateArray().Single().GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task Capture_routes_require_authentication()
    {
        var client = new Client(factory);
        var response = await client.Raw("/sales/item-tag-captures/" + Guid.NewGuid());
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401, got {(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var upload = await client.SendBytes("/sales/item-tag-captures", Jpeg, "image/jpeg", Key());
        Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
    }
}
