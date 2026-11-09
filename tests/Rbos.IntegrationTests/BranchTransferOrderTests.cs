using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class BranchTransferOrderTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<JsonElement> Body(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();
    static async Task<Guid> Id(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await Body(r)).GetProperty("id").GetGuid(); }

    [Fact]
    public async Task Branches_can_be_created_listed_and_given_locations()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();

        var created = await c.Send(HttpMethod.Post, "/branches", new { name = "Andheri", locationName = (string?)null });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var branchId = (await Body(created)).GetProperty("id").GetGuid();

        var more = await c.Send(HttpMethod.Post, "/locations", new { branchId, name = "Back room" });
        Assert.Equal(HttpStatusCode.OK, more.StatusCode);

        var list = await c.GetJson("/branches");
        var branch = list.EnumerateArray().First(b => b.GetProperty("id").GetGuid() == branchId);
        Assert.Equal("Andheri", branch.GetProperty("name").GetString());
        Assert.False(branch.GetProperty("isPrimary").GetBoolean());
        Assert.Equal(2, branch.GetProperty("locations").GetArrayLength());
    }

    [Fact]
    public async Task Branch_and_location_validation_and_login_are_enforced()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/branches", new { name = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/branches", new { name = new string('x', 121) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Send(HttpMethod.Post, "/locations", new { branchId = Guid.NewGuid(), name = "Ghost" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/branches")).StatusCode);
    }

    [Fact]
    public async Task Branches_are_isolated_between_businesses()
    {
        var a = new Client(f); await a.SignUpAndBootstrap();
        var b = new Client(f); await b.SignUpAndBootstrap();
        await Id(await a.Send(HttpMethod.Post, "/branches", new { name = "Only A" }));
        var seenByB = await b.GetJson("/branches");
        Assert.DoesNotContain(seenByB.EnumerateArray(), x => x.GetProperty("name").GetString() == "Only A");
    }

    [Fact]
    public async Task Stock_transfer_moves_stock_between_locations_and_rejects_bad_requests()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var main = await c.Location(); var p = await c.AddProduct("Charger");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = main, receiptDate = Today, lines = new[] { new { productId = p, quantity = 10m, unitCost = 20m } } }, K()));
        var other = (await Body(await c.Send(HttpMethod.Post, "/branches", new { name = "Powai" }))).GetProperty("locationId").GetGuid();

        var ok = await c.Send(HttpMethod.Post, "/stock-transfers", new { date = Today, fromLocationId = main, toLocationId = other, note = "restock", lines = new[] { new { productId = p, quantity = 4m } } });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(1, (await c.GetJson("/stock-transfers")).GetArrayLength());

        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/stock-transfers", new { date = Today, fromLocationId = main, toLocationId = other, lines = new[] { new { productId = p, quantity = 7m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/stock-transfers", new { date = Today, fromLocationId = main, toLocationId = main, lines = new[] { new { productId = p, quantity = 1m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/stock-transfers", new { date = Today, fromLocationId = main, toLocationId = other, lines = new[] { new { productId = p, quantity = 0m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/stock-transfers", new { date = Today, fromLocationId = main, toLocationId = other, lines = Array.Empty<object>() })).StatusCode);
        Assert.Equal(1, (await c.GetJson("/stock-transfers")).GetArrayLength());

        var verify = await c.GetJson("/reports/stock-verification");
        Assert.True(verify.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Purchase_order_lifecycle_creates_lists_closes_once_and_validates()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var p = await c.AddProduct("Adapter");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Globex" }));

        var created = await c.Send(HttpMethod.Post, "/purchase-orders", new { supplierId = sup, date = Today, expectedDate = Today, note = "first", lines = new[] { new { productId = p, quantity = 5m, unitCost = 12m } } });
        var orderId = await Id(created);
        Assert.Equal(1, (await c.GetJson("/purchase-orders")).GetArrayLength());

        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, $"/purchase-orders/{orderId}/status", new { status = "Done" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, $"/purchase-orders/{orderId}/status", new { status = "Received" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Put, $"/purchase-orders/{orderId}/status", new { status = "Cancelled" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Put, $"/purchase-orders/{Guid.NewGuid()}/status", new { status = "Received" })).StatusCode);
    }

    [Fact]
    public async Task Purchase_order_rejects_bad_lines_and_unknown_supplier()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var p = await c.AddProduct("Lamp");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Initech" }));

        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/purchase-orders", new { supplierId = sup, date = Today, lines = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/purchase-orders", new { supplierId = sup, date = Today, lines = new[] { new { productId = p, quantity = -1m, unitCost = 5m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/purchase-orders", new { supplierId = sup, date = Today, lines = new[] { new { productId = p, quantity = 1m, unitCost = 5m }, new { productId = p, quantity = 2m, unitCost = 5m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Send(HttpMethod.Post, "/purchase-orders", new { supplierId = Guid.NewGuid(), date = Today, lines = new[] { new { productId = p, quantity = 1m, unitCost = 5m } } })).StatusCode);
        Assert.Equal(0, (await c.GetJson("/purchase-orders")).GetArrayLength());
    }
}
