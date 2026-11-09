using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class StockInsightsAndCountTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<JsonElement> Body(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Unit_conversions_are_validated_and_listed()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var p = await c.AddProduct("Rope");

        var ok = await c.Send(HttpMethod.Put, "/unit-conversions", new { productId = p, unit = "box", factor = 12m });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, "/unit-conversions", new { productId = p, unit = "box", factor = 0m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, "/unit-conversions", new { productId = p, unit = "box", factor = 1.23456m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, "/unit-conversions", new { productId = p, unit = "", factor = 2m })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Send(HttpMethod.Put, "/unit-conversions", new { productId = Guid.NewGuid(), unit = "box", factor = 2m })).StatusCode);

        var list = await c.GetJson($"/unit-conversions?productId={p}");
        Assert.Contains(list.EnumerateArray(), r => r.GetProperty("unit").GetString() == "box" && r.GetProperty("factor").GetDecimal() == 12m);
    }

    [Fact]
    public async Task Stock_count_requires_key_lines_and_valid_quantities()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location();
        var p = await c.AddProduct("Bulb");

        object Req(decimal counted) => new { locationId = loc, date = Today, lines = new[] { new { productId = p, counted } } };

        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/stock-count", Req(1m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/stock-count", new { locationId = loc, date = Today, lines = Array.Empty<object>() }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/stock-count", Req(-1m), K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/stock-count", Req(1.2345m), K())).StatusCode);
    }

    [Fact]
    public async Task Stock_count_adjusts_to_counted_quantity_and_replays_with_same_key()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location();
        var p = await c.AddProduct("Switch");
        var sup = (await Body(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Count Supplier " + K() }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 10m, unitCost = 5m } } }, K())).StatusCode);

        var key = K();
        var req = new { locationId = loc, date = Today, lines = new[] { new { productId = p, counted = 7m } } };
        var first = await c.Send(HttpMethod.Post, "/stock-count", req, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(1, (await Body(first)).GetProperty("adjusted").GetInt32());

        var replay = await c.Send(HttpMethod.Post, "/stock-count", req, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        var sheet = await c.GetJson($"/stock-count/sheet?locationId={loc}");
        var row = sheet.EnumerateArray().First(r => r.GetProperty("productId").GetGuid() == p);
        Assert.Equal(7m, row.GetProperty("system").GetDecimal());

        var history = await c.GetJson($"/stock-adjustments/history?productId={p}");
        Assert.Equal(1, history.GetArrayLength());
        Assert.Equal(-3m, history[0].GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task Reorder_levels_drive_the_low_stock_list_and_dead_and_ageing_reports_load()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location();
        var p = await c.AddProduct("Fuse");
        var sup = (await Body(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Low Supplier " + K() }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 2m, unitCost = 5m } } }, K())).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, "/reorder-levels", new { productId = p, level = 5m })).StatusCode);
        var low = await c.GetJson("/stock/low");
        Assert.Contains(low.EnumerateArray(), r => r.GetProperty("productId").GetGuid() == p);

        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Get, "/stock/dead?days=30")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Get, "/stock/ageing")).StatusCode);
    }

    [Fact]
    public async Task Stock_insights_require_login_and_are_isolated_between_businesses()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/stock/low")).StatusCode);

        var a = new Client(f); await a.SignUpAndBootstrap();
        var b = new Client(f); await b.SignUpAndBootstrap();
        var p = await a.AddProduct("Private item");
        Assert.Equal(HttpStatusCode.OK, (await a.Send(HttpMethod.Put, "/reorder-levels", new { productId = p, level = 5m })).StatusCode);
        var lowB = await b.GetJson("/stock/low");
        Assert.DoesNotContain(lowB.EnumerateArray(), r => r.GetProperty("productId").GetGuid() == p);
    }
}
