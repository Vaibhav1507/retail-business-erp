using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class FinanceAndPeriodLockTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<Guid> Id(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); }

    async Task<(Client c, Guid loc, Guid p, Guid money)> Stocked()
    {
        var c = new Client(f); await c.SignUpAndBootstrap(); var loc = await c.Location(); var p = await c.AddProduct("Cable");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 10m, unitCost = 20m } } }, K()));
        var money = (await c.GetJson("/money-accounts"))[0].GetProperty("id").GetGuid();
        return (c, loc, p, money);
    }

    static async Task AssertBooksBalance(Client c)
    {
        var tb = await c.GetJson("/reports/trial-balance");
        Assert.Equal(tb.GetProperty("totalDebit").GetDecimal(), tb.GetProperty("totalCredit").GetDecimal());
        Assert.True((await c.GetJson("/reports/balance-sheet")).GetProperty("balanced").GetBoolean());
    }

    [Fact] public async Task Capital_expense_drawing_keep_books_balanced_and_totals_reconcile()
    {
        var (c, _, _, m) = await Stocked();
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/owner-transactions", new { kind = "Capital", date = Today, moneyAccountId = m, amount = 1000m }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/expenses", new { date = Today, category = "Rent", moneyAccountId = m, amount = 300m }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/owner-transactions", new { kind = "Drawing", date = Today, moneyAccountId = m, amount = 200m }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/owner-transactions", new { kind = "Drawing", date = Today, moneyAccountId = m, amount = 600m }, K())).StatusCode);
        Assert.Equal(500m, (await c.GetJson("/money-accounts"))[0].GetProperty("balance").GetDecimal());
        Assert.Equal(-300m, (await c.GetJson("/reports/profit-loss")).GetProperty("netProfit").GetDecimal());
        Assert.Equal(-300m, (await c.GetJson("/reports/balance-sheet")).GetProperty("currentYearResult").GetDecimal());
        await AssertBooksBalance(c);
        var key = K(); var body = new { date = Today, category = "Utilities", moneyAccountId = m, amount = 10m };
        await Id(await c.Send(HttpMethod.Post, "/expenses", body, key));
        Assert.True((await (await c.Send(HttpMethod.Post, "/expenses", body, key)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replayed").GetBoolean());
        Assert.Equal(2, (await c.GetJson("/expenses")).GetArrayLength());
    }

    [Fact] public async Task Stock_adjustment_requires_reason_and_updates_stock_value_and_books()
    {
        var (c, loc, p, _) = await Stocked();
        object Adj(decimal q, string reason, string dir = "Decrease", string? note = null) => new { productId = p, locationId = loc, date = Today, quantity = q, direction = dir, reason, note };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/stock-adjustments", Adj(1, ""), K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/stock-adjustments", Adj(1, "Other"), K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/stock-adjustments", Adj(11, "Damage"), K())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/stock-adjustments", Adj(2, "Damage"), K())).StatusCode);
        Assert.Equal(160m, (await c.GetJson("/reports/stock-valuation")).GetProperty("totalValue").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/stock-adjustments", Adj(1, "FoundOnCount", "Increase"), K())).StatusCode);
        Assert.Equal(180m, (await c.GetJson("/reports/stock-valuation")).GetProperty("totalValue").GetDecimal());
        Assert.Equal(9m, (await c.GetJson("/stock")).EnumerateArray().Single().GetProperty("quantity").GetDecimal());
        await AssertBooksBalance(c);
    }

    [Fact] public async Task Period_lock_blocks_postings_and_backward_move_needs_reason()
    {
        var (c, _, _, m) = await Stocked();
        object Exp() => new { date = Today, category = "Rent", moneyAccountId = m, amount = 5m };
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, "/period-lock", new { lockedThrough = Today }, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/expenses", Exp(), K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, "/period-lock", new { lockedThrough = (string?)null }, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, "/period-lock", new { lockedThrough = (string?)null, reason = "Correction" }, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/expenses", Exp(), K())).StatusCode);
    }
}
