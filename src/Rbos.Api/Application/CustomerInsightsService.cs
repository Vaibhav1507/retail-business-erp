using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

/// <summary>Phase 2 C6 customer purchase history and C9 customer dues ageing.</summary>
public sealed class CustomerInsightsService(TenantDb db)
{
    static readonly (string Label, int Max)[] Buckets = [("0-30", 30), ("31-60", 60), ("61-90", 90), ("90+", int.MaxValue)];

    public async Task<ServiceResult<object>> HistoryAsync(Guid uid, Guid customerId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);

        string? name = null;
        await using (var c = s.Command("select name from sales.customer where id=@c"))
        {
            c.Parameters.AddWithValue("c", customerId);
            name = await c.ExecuteScalarAsync(ct) as string;
        }
        if (name is null) return ServiceResult<object>.Fail("Customer not found.", 404);

        var sales = new List<object>();
        await using (var c = s.Command(@"select x.id, x.number, x.sale_date, x.total, x.amount_paid,
              exists (select 1 from sales.sale_cancellation k where k.sale_id=x.id),
              coalesce((select sum(quantity) from sales.sale_line l where l.sale_id=x.id),0)
            from sales.sale x where x.customer_id=@c order by x.sale_date desc, x.number desc"))
        {
            c.Parameters.AddWithValue("c", customerId);
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                sales.Add(new { id = r.GetGuid(0), number = r.GetInt64(1), date = r.GetFieldValue<DateOnly>(2), total = r.GetDecimal(3), paid = r.GetDecimal(4), cancelled = r.GetBoolean(5), quantity = r.GetDecimal(6) });
        }

        var top = new List<object>();
        await using (var c = s.Command(@"select p.id, p.name, sum(l.quantity), sum(l.line_total), max(x.sale_date)
            from sales.sale x join sales.sale_line l on l.sale_id=x.id join catalog.product p on p.id=l.product_id
            where x.customer_id=@c and not exists (select 1 from sales.sale_cancellation k where k.sale_id=x.id)
            group by p.id, p.name order by sum(l.line_total) desc, p.name limit 10"))
        {
            c.Parameters.AddWithValue("c", customerId);
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                top.Add(new { productId = r.GetGuid(0), product = r.GetString(1), quantity = r.GetDecimal(2), amount = r.GetDecimal(3), lastSold = r.GetFieldValue<DateOnly>(4) });
        }
        return ServiceResult<object>.Ok(new { customer = name, sales, topProducts = top });
    }

    public async Task<ServiceResult<object>> DuesAgeingAsync(Guid uid, DateOnly today, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);

        var names = new Dictionary<Guid, (string Name, string Phone)>();
        await using (var c = s.Command("select id, name, coalesce(phone,'') from sales.customer"))
        {
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) names[r.GetGuid(0)] = (r.GetString(1), r.GetString(2));
        }

        var dues = new Dictionary<Guid, List<(DateOnly Date, decimal Due)>>();
        await using (var c = s.Command(@"select customer_id, sale_date, total - amount_paid from sales.sale x
            where customer_id is not null and not exists (select 1 from sales.sale_cancellation k where k.sale_id=x.id)
            order by sale_date, number"))
        {
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var id = r.GetGuid(0);
                if (!dues.TryGetValue(id, out var l)) dues[id] = l = [];
                l.Add((r.GetFieldValue<DateOnly>(1), r.GetDecimal(2)));
            }
        }

        var credits = new Dictionary<Guid, decimal>();
        await using (var c = s.Command(@"select customer_id, sum(v) from (
              select customer_id, amount v from sales.customer_payment
              union all select customer_id, receivable_credit from sales.sale_return where customer_id is not null) t group by customer_id"))
        {
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) credits[r.GetGuid(0)] = r.GetDecimal(1);
        }

        var result = new List<object>();
        foreach (var (id, list) in dues)
        {
            var credit = credits.GetValueOrDefault(id);
            var buckets = new decimal[Buckets.Length];
            foreach (var (date, due) in list)
            {
                var open = due;
                if (credit > 0) { var used = Math.Min(credit, open); credit -= used; open -= used; }
                if (open <= 0) continue;
                var age = today.DayNumber - date.DayNumber;
                buckets[Array.FindIndex(Buckets, b => age <= b.Max)] += open;
            }
            var total = buckets.Sum();
            if (total <= 0 || !names.TryGetValue(id, out var n)) continue;
            result.Add(new { customerId = id, customer = n.Name, phone = n.Phone, outstanding = total, buckets = Buckets.Select((b, i) => new { label = b.Label, amount = buckets[i] }).ToList() });
        }
        return ServiceResult<object>.Ok(result.OrderByDescending(x => ((dynamic)x).outstanding).ToList());
    }
}
