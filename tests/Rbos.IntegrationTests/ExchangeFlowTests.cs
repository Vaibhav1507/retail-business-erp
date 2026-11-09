using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class ExchangeFlowTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<JsonElement> Body(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();
    static async Task<Guid> Id(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await Body(r)).GetProperty("id").GetGuid(); }

    [Fact]
    public async Task Exchange_is_a_return_of_the_old_item_plus_a_new_bill_and_stock_follows()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location();
        var oldItem = await c.AddProduct("Old charger");
        var newItem = await c.AddProduct("New charger");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Exchange Supplier " + K() }));
        var cust = await Id(await c.Send(HttpMethod.Post, "/customers", new { name = "Exchange Customer " + K() }));

        foreach (var p in new[] { oldItem, newItem })
            Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 10m, unitCost = 20m } } }, K())).StatusCode);

        var sale = await Id(await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 0m, lines = new[] { new { productId = oldItem, quantity = 2m, unitPrice = 50m, discount = 0m } } }, K()));

        var ret = await c.Send(HttpMethod.Post, "/sale-returns", new { saleId = sale, returnDate = Today, reason = "Exchange", lines = new[] { new { productId = oldItem, quantity = 2m } } }, K());
        Assert.Equal(HttpStatusCode.OK, ret.StatusCode);

        var replacement = await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 0m, lines = new[] { new { productId = newItem, quantity = 2m, unitPrice = 60m, discount = 0m } } }, K());
        Assert.Equal(HttpStatusCode.OK, replacement.StatusCode);

        var stock = await c.GetJson("/stock");
        decimal Qty(Guid p) => stock.EnumerateArray().Where(r => r.GetProperty("productId").GetGuid() == p).Sum(r => r.GetProperty("quantity").GetDecimal());
        Assert.Equal(10m, Qty(oldItem));
        Assert.Equal(8m, Qty(newItem));
    }

    [Fact]
    public async Task Exchange_return_is_rejected_when_quantity_exceeds_what_was_sold_and_leaves_stock_unchanged()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location();
        var p = await c.AddProduct("Limited item");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Limit Supplier " + K() }));
        var cust = await Id(await c.Send(HttpMethod.Post, "/customers", new { name = "Limit Customer " + K() }));
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 5m, unitCost = 20m } } }, K())).StatusCode);
        var sale = await Id(await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 0m, lines = new[] { new { productId = p, quantity = 1m, unitPrice = 50m, discount = 0m } } }, K()));

        var over = await c.Send(HttpMethod.Post, "/sale-returns", new { saleId = sale, returnDate = Today, reason = "Exchange", lines = new[] { new { productId = p, quantity = 3m } } }, K());
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);

        var stock = await c.GetJson("/stock");
        Assert.Equal(4m, stock.EnumerateArray().Where(r => r.GetProperty("productId").GetGuid() == p).Sum(r => r.GetProperty("quantity").GetDecimal()));
    }

    [Fact]
    public async Task Returning_against_an_unknown_sale_is_rejected()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var p = await c.AddProduct("Ghost item");
        var res = await c.Send(HttpMethod.Post, "/sale-returns", new { saleId = Guid.NewGuid(), returnDate = Today, reason = "Exchange", lines = new[] { new { productId = p, quantity = 1m } } }, K());
        Assert.True(res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
    }
}
