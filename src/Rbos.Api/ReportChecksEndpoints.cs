using System.Security.Claims;
using Rbos.Api.Persistence;

namespace Rbos.Api;

/// <summary>A8 cash flow statement and A12 WAC verification. Read-only reports.</summary>
public static class ReportChecksEndpoints
{
    static readonly string[] FinancingSources = ["Capital", "Drawing", "Drawings"];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/reports/cash-flow", async (DateOnly from, DateOnly to, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            if (to < from) return ApiResults.Fail(400, "'to' must not be before 'from'.");
            var tid = await db.FindTenantAsync(Uid(p), ct);
            if (tid is null) return ApiResults.Fail(409, "No business set up.");
            await using var s = await db.BeginAsync(Uid(p), tid, ct);

            decimal opening;
            await using (var c = s.Command(@"select coalesce(sum(l.debit-l.credit),0) from accounting.journal_line l
                join accounting.journal_entry e on e.id=l.entry_id
                where e.effective_date<@f and l.account_id in (select account_id from accounting.money_account)"))
            {
                c.Parameters.AddWithValue("f", from);
                opening = (decimal)(await c.ExecuteScalarAsync(ct) ?? 0m);
            }

            var lines = new List<(string Source, decimal Net)>();
            await using (var c = s.Command(@"select e.source_type, coalesce(sum(l.debit-l.credit),0)
                from accounting.journal_line l join accounting.journal_entry e on e.id=l.entry_id
                where e.effective_date between @f and @t and l.account_id in (select account_id from accounting.money_account)
                group by e.source_type order by e.source_type"))
            {
                c.Parameters.AddWithValue("f", from);
                c.Parameters.AddWithValue("t", to);
                await using var r = await c.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) lines.Add((r.GetString(0), r.GetDecimal(1)));
            }

            var operating = lines.Where(x => !FinancingSources.Contains(x.Source) && x.Net != 0).Select(x => new { source = x.Source, amount = x.Net }).ToList();
            var financing = lines.Where(x => FinancingSources.Contains(x.Source) && x.Net != 0).Select(x => new { source = x.Source, amount = x.Net }).ToList();
            var netOperating = operating.Sum(x => x.amount);
            var netFinancing = financing.Sum(x => x.amount);
            return Results.Ok(new
            {
                from, to, openingCash = opening, operating, netOperating, financing, netFinancing,
                netChange = netOperating + netFinancing, closingCash = opening + netOperating + netFinancing
            });
        }).RequireAuthorization();

        api.MapGet("/reports/wac-verification", async (ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var tid = await db.FindTenantAsync(Uid(p), ct);
            if (tid is null) return ApiResults.Fail(409, "No business set up.");
            await using var s = await db.BeginAsync(Uid(p), tid, ct);
            var issues = new List<object>();
            await using var c = s.Command(@"select pr.id, pr.name, ic.total_quantity, ic.inventory_value, ic.wac,
                  coalesce((select sum(b.quantity) from inventory.stock_balance b where b.product_id=ic.product_id),0)
                from inventory.item_cost ic join catalog.product pr on pr.id=ic.product_id order by pr.name");
            await using var r = await c.ExecuteReaderAsync(ct);
            var checkedCount = 0;
            while (await r.ReadAsync(ct))
            {
                checkedCount++;
                var qty = r.GetDecimal(2); var value = r.GetDecimal(3); var wac = r.GetDecimal(4); var onHand = r.GetDecimal(5);
                var expected = qty == 0 ? 0m : Math.Round(value / qty, 4);
                var problems = new List<string>();
                if (qty != onHand) problems.Add($"item_cost quantity {qty} differs from stock balance {onHand}");
                if (Math.Abs(wac - expected) > 0.0001m) problems.Add($"wac {wac} differs from value/quantity {expected}");
                if (problems.Count > 0) issues.Add(new { productId = r.GetGuid(0), name = r.GetString(1), problems });
            }
            return Results.Ok(new { checkedProducts = checkedCount, ok = issues.Count == 0, issues });
        }).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
