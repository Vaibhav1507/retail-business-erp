using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class PurchaseGstTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();

    static async Task<Client> NewClient(ApiFactory f, string gstStatus)
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var gstin = gstStatus is "Regular" or "Composition" ? "27AAPFU0939F1ZV" : null;
        var profile = new { businessName = "GST Traders", businessCategory = "general_retail", ownerName = "Owner", address = "1 Test Road", city = "Bengaluru", state = "Karnataka", pinCode = "560001", gstStatus, gstin, financialYearStartMonth = 4, language = "en" };
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, "/business-profile", profile)).StatusCode);
        return c;
    }

    static async Task<Guid> Supplier(Client c)
    {
        var r = await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme Traders" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    static object Receipt(Guid sup, Guid loc, Guid product, decimal qty, decimal cost, decimal gst)
        => new { supplierId = sup, locationId = loc, receiptDate = Today, supplierRef = "INV-GST", lines = new[] { new { productId = product, quantity = qty, unitCost = cost, gstAmount = gst } } };

    static async Task<decimal> Owed(Client c, Guid sup)
        => (await c.GetJson("/suppliers")).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == sup).GetProperty("owed").GetDecimal();

    [Fact]
    public async Task Composition_business_owes_full_bill_and_stock_cost_includes_gst()
    {
        var c = await NewClient(f, "Composition"); var sup = await Supplier(c); var loc = await c.Location(); var p = await c.AddProduct("Battery");
        var r = await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 10, 100m, 180m), K());
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(1180m, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
        Assert.Equal(1180m, await Owed(c, sup));
    }

    [Fact]
    public async Task Regular_business_owes_full_bill_including_gst()
    {
        var c = await NewClient(f, "Regular"); var sup = await Supplier(c); var loc = await c.Location(); var p = await c.AddProduct("Battery");
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 10, 100m, 180m), K())).StatusCode);
        Assert.Equal(1180m, await Owed(c, sup));
    }

    [Fact]
    public async Task Gst_larger_than_line_value_is_rejected()
    {
        var c = await NewClient(f, "Composition"); var sup = await Supplier(c); var loc = await c.Location(); var p = await c.AddProduct("Battery");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 1, 100m, 500m), K())).StatusCode);
    }

    [Fact]
    public async Task Purchase_without_gst_still_posts_at_pre_tax_cost()
    {
        var c = await NewClient(f, "NotRegistered"); var sup = await Supplier(c); var loc = await c.Location(); var p = await c.AddProduct("Cable");
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 5, unitCost = 20m } } }, K())).StatusCode);
        Assert.Equal(100m, await Owed(c, sup));
    }

    [Fact]
    public async Task Returning_goods_reduces_amount_owed_by_the_gst_inclusive_value()
    {
        var c = await NewClient(f, "Regular"); var sup = await Supplier(c); var loc = await c.Location(); var p = await c.AddProduct("Battery");
        var rec = await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 10, 100m, 180m), K());
        var receiptId = (await rec.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var ret = await c.Send(HttpMethod.Post, "/purchase-returns", new { receiptId, returnDate = Today, reason = "Faulty", lines = new[] { new { productId = p, quantity = 5m } } }, K());
        Assert.Equal(HttpStatusCode.OK, ret.StatusCode);
        Assert.Equal(590m, (await ret.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
        Assert.Equal(590m, await Owed(c, sup));
    }
}
