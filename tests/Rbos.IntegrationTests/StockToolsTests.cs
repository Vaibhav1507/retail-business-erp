using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class StockToolsTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();

    [Fact] public async Task Stock_count_adjustments_history_low_dead_and_ageing_work_together()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var p = await c.AddProduct("Count Widget");
        var loc = await c.Location();
        var open = await c.Send(HttpMethod.Post, "/opening-stock", new { locationId = loc, effectiveDate = Today, lines = new[] { new { productId = p, quantity = 10m, unitCost = 25.5m } } }, K());
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);

        // B1 unit conversion
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, "/unit-conversions", new { productId = p, unit = "box", factor = 12m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, "/unit-conversions", new { productId = p, unit = "box", factor = 0m })).StatusCode);
        var conv = await c.GetJson("/unit-conversions?productId=" + p);
        Assert.Equal(1, conv.GetArrayLength());
        Assert.Equal(12m, conv[0].GetProperty("factor").GetDecimal());

        // B2 physical count
        var sheet = await c.GetJson("/stock-count/sheet?locationId=" + loc);
        Assert.Equal(10m, sheet[0].GetProperty("system").GetDecimal());
        var bad = await c.Send(HttpMethod.Post, "/stock-count", new { locationId = loc, date = Today, lines = new[] { new { productId = p, counted = -1m } } }, K());
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var counted = await c.Send(HttpMethod.Post, "/stock-count", new { locationId = loc, date = Today, lines = new[] { new { productId = p, counted = 7m } } }, K());
        Assert.Equal(HttpStatusCode.OK, counted.StatusCode);
        Assert.Equal(1, (await counted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("adjusted").GetInt32());
        Assert.Equal(7m, (await c.GetJson("/stock-count/sheet?locationId=" + loc))[0].GetProperty("system").GetDecimal());

        // B3 history
        var hist = await c.GetJson("/stock-adjustments/history?productId=" + p + "&reason=Shortage");
        Assert.Equal(1, hist.GetArrayLength());
        Assert.Equal(-3m, hist[0].GetProperty("quantity").GetDecimal());
        Assert.Equal(0, (await c.GetJson("/stock-adjustments/history?reason=Theft")).GetArrayLength());

        // B4 low stock
        Assert.Equal(0, (await c.GetJson("/stock/low")).GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, "/reorder-levels", new { productId = p, level = 20m })).StatusCode);
        var low = await c.GetJson("/stock/low");
        Assert.Equal(1, low.GetArrayLength());
        Assert.Equal(7m, low[0].GetProperty("onHand").GetDecimal());

        // B5 dead stock (never sold)
        var dead = await c.GetJson("/stock/dead?days=30");
        Assert.Equal(1, dead.GetArrayLength());
        Assert.Equal(178.5m, dead[0].GetProperty("value").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Get, "/stock/dead?days=0")).StatusCode);

        // B6 ageing: all stock received today falls in the 0-30 bucket
        var ageing = await c.GetJson("/stock/ageing");
        Assert.Equal(1, ageing.GetArrayLength());
        Assert.Equal(7m, ageing[0].GetProperty("buckets")[0].GetProperty("quantity").GetDecimal());
    }
}
