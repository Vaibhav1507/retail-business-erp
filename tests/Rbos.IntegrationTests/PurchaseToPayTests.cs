using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class PurchaseToPayTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static async Task<Client> NewClient(ApiFactory f) { var c = new Client(f); await c.SignUpAndBootstrap(); return c; }
    static async Task<Guid> Supplier(Client c, string name = "Acme Traders")
    {
        var r = await c.Send(HttpMethod.Post, "/suppliers", new { name });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    static object Receipt(Guid supplier, Guid loc, Guid product, decimal qty, decimal cost)
        => new { supplierId = supplier, locationId = loc, receiptDate = Today, supplierRef = "INV-1", lines = new[] { new { productId = product, quantity = qty, unitCost = cost } } };
    static async Task<decimal> Owed(Client c, Guid supplier)
        => (await c.GetJson("/suppliers")).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == supplier).GetProperty("owed").GetDecimal();
    static string K() => Guid.NewGuid().ToString();

    [Fact] public async Task Supplier_names_unique_ignoring_case()
    {
        var c = await NewClient(f); await Supplier(c);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/suppliers", new { name = "  acme traders " })).StatusCode);
    }
    [Fact] public async Task Receipt_increases_stock_updates_wac_and_creates_payable()
    {
        var c = await NewClient(f); var sup = await Supplier(c); var loc = await c.Location(); var p = await c.AddProduct("Cable");
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 10, 20m), K())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 10, 30m), K())).StatusCode);
        var row = (await c.GetJson("/stock")).EnumerateArray().Single();
        Assert.Equal(20m, row.GetProperty("quantity").GetDecimal());
        Assert.Equal(25m, row.GetProperty("wac").GetDecimal());
        Assert.Equal(500m, row.GetProperty("value").GetDecimal());
        Assert.Equal(500m, await Owed(c, sup));
    }
    [Fact] public async Task Receipt_idempotent_and_key_reuse_conflicts()
    {
        var c = await NewClient(f); var sup = await Supplier(c); var loc = await c.Location(); var p = await c.AddProduct("Cable"); var key = K();
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 5, 10m), key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 5, 10m), key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 6, 10m), key)).StatusCode);
        Assert.Equal(50m, await Owed(c, sup));
    }
    [Fact] public async Task Receipt_rejects_bad_input()
    {
        var c = await NewClient(f); var sup = await Supplier(c); var loc = await c.Location(); var p = await c.AddProduct("Cable");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 0, 10m), K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 1, 0m), K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/purchases", Receipt(Guid.NewGuid(), loc, p, 1, 5m), K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 1, 5m))).StatusCode);
    }
    [Fact] public async Task Payment_reduces_payable_and_cannot_exceed_it()
    {
        var c = await NewClient(f); var sup = await Supplier(c); var loc = await c.Location(); var p = await c.AddProduct("Cable");
        await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 10, 10m), K());
        object Pay(decimal a) => new { supplierId = sup, paymentDate = Today, amount = a, method = "UPI", reference = "ref" };
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/supplier-payments", Pay(40m), K())).StatusCode);
        Assert.Equal(60m, await Owed(c, sup));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/supplier-payments", Pay(60.01m), K())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/supplier-payments", Pay(60m), K())).StatusCode);
        Assert.Equal(0m, await Owed(c, sup));
    }
    [Fact] public async Task Payment_is_idempotent()
    {
        var c = await NewClient(f); var sup = await Supplier(c); var loc = await c.Location(); var p = await c.AddProduct("Cable");
        await c.Send(HttpMethod.Post, "/purchases", Receipt(sup, loc, p, 10, 10m), K());
        var key = K(); var body = new { supplierId = sup, paymentDate = Today, amount = 30m, method = "Cash" };
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/supplier-payments", body, key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/supplier-payments", body, key)).StatusCode);
        Assert.Equal(70m, await Owed(c, sup));
    }
    [Fact] public async Task Other_business_cannot_see_or_pay_suppliers()
    {
        var a = await NewClient(f); var b = await NewClient(f); var sup = await Supplier(a);
        Assert.Empty((await b.GetJson("/suppliers")).EnumerateArray());
        var r = await b.Send(HttpMethod.Post, "/supplier-payments", new { supplierId = sup, paymentDate = Today, amount = 1m, method = "Cash" }, K());
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
