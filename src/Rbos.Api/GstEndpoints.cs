using System.Security.Claims;
using Rbos.Api.Persistence;
using Rbos.Domain;
using static Rbos.Api.ApiResults;

namespace Rbos.Api;

/// <summary>A1/G3: GST output-tax summary by rate, built from posted (non-cancelled) GST invoices. Read-only.</summary>
public static class GstEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/reports/gst-summary", async (DateOnly from, DateOnly to, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            if (to < from) return Fail(400, "'to' must not be before 'from'.");
            var uid = Uid(p);
            var tid = await db.FindTenantAsync(uid, ct);
            if (tid is null) return Fail(409, "No business set up.");
            await using var s = await db.BeginAsync(uid, tid, ct);

            var rows = new List<(decimal Rate, bool Inter, int Invoices, decimal Gross)>();
            await using (var c = s.Command(@"select x.gst_rate, x.is_inter_state, count(*), coalesce(sum(x.total),0)
                from sales.sale x
                where x.sale_date between @f and @t and x.gst_rate is not null
                  and not exists (select 1 from sales.sale_cancellation k where k.sale_id = x.id)
                group by x.gst_rate, x.is_inter_state order by x.gst_rate, x.is_inter_state"))
            {
                c.Parameters.AddWithValue("f", from);
                c.Parameters.AddWithValue("t", to);
                await using var r = await c.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) rows.Add((r.GetDecimal(0), r.GetBoolean(1), (int)r.GetInt64(2), r.GetDecimal(3)));
            }

            var lines = rows.Select(x =>
            {
                var g = GstCalculator.FromInclusive(x.Gross, x.Rate, x.Inter);
                return new { rate = x.Rate, interState = x.Inter, igst = g.Igst, invoices = x.Invoices, taxable = g.Taxable, cgst = g.Cgst, sgst = g.Sgst, tax = g.Tax, total = g.Total };
            }).ToList();
            return Results.Ok(new
            {
                from, to, lines,
                totals = new { taxable = lines.Sum(l => l.taxable), cgst = lines.Sum(l => l.cgst), sgst = lines.Sum(l => l.sgst), igst = lines.Sum(l => l.igst), tax = lines.Sum(l => l.tax) },
                note = "Intra-state = CGST+SGST; inter-state (customer state differs from business state) = IGST. Draft figures for review; not a filed return."
            });
        });
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
