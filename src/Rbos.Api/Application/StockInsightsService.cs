using Rbos.Api.Persistence;
using static Rbos.Api.Application.SqlHelpers;

namespace Rbos.Api.Application;

public sealed record UnitConversionCommand(Guid ProductId, string Unit, decimal Factor);
public sealed record ReorderLevelCommand(Guid ProductId, decimal Level);
public sealed record StockCountLine(Guid ProductId, decimal Counted);
public sealed record StockCountCommand(Guid LocationId, DateOnly Date, List<StockCountLine> Lines);

/// <summary>Phase 2 B1-B6: unit conversions, physical stock count, adjustment history, low stock, dead stock and ageing.</summary>
public sealed class StockInsightsService(TenantDb db, FinanceService finance)
{
    static readonly (string Label, int From, int To)[] Buckets = [("0-30", 0, 30), ("31-60", 31, 60), ("61-90", 61, 90), ("90+", 91, int.MaxValue)];

    async Task<(TenantSession? S, Guid Tenant, ServiceResult<object>? Err)> OpenAsync(Guid uid, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return (null, default, ServiceResult<object>.Fail("No business set up.", 409));
        return (await db.BeginAsync(uid, tid, ct), tid.Value, null);
    }

    static async Task<List<object[]>> Rows(TenantSession s, string sql, CancellationToken ct, params (string, object?)[] ps)
    {
        await using var c = s.Command(sql);
        foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
        await using var r = await c.ExecuteReaderAsync(ct);
        var list = new List<object[]>();
        while (await r.ReadAsync(ct))
        {
            var row = new object[r.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                var v = r.GetValue(i);
                row[i] = v is DBNull ? "" : v is DateTime dt ? DateOnly.FromDateTime(dt) : v;
            }
            list.Add(row);
        }
        return list;
    }

    // ---------- B1 unit conversions ----------
    public async Task<ServiceResult<object>> UnitConversionsAsync(Guid uid, Guid? productId, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        var rows = await Rows(s!, @"select u.product_id, p.name, p.base_unit, u.unit, u.factor from inventory.unit_conversion u
            join catalog.product p on p.tenant_id=u.tenant_id and p.id=u.product_id
            where (@p::uuid is null or u.product_id=@p) order by p.name, u.unit", ct, ("p", productId));
        return ServiceResult<object>.Ok(rows.Select(r => new { productId = r[0], product = r[1], baseUnit = r[2], unit = r[3], factor = r[4] }).ToList());
    }

    public async Task<ServiceResult<object>> SetUnitConversionAsync(Guid uid, UnitConversionCommand m, CancellationToken ct)
    {
        var unit = (m.Unit ?? "").Trim();
        if (unit.Length is < 1 or > 20) return ServiceResult<object>.Fail("Unit must be 1-20 characters.");
        if (m.Factor <= 0 || decimal.Round(m.Factor, 4) != m.Factor) return ServiceResult<object>.Fail("Factor must be positive with at most 4 decimals.");
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        var exists = await Rows(s!, "select base_unit from catalog.product where id=@p", ct, ("p", m.ProductId));
        if (exists.Count == 0) return ServiceResult<object>.Fail("Product not found.", 404);
        if (string.Equals(unit, (string)exists[0][0], StringComparison.OrdinalIgnoreCase)) return ServiceResult<object>.Fail("Unit equals the base unit.");
        await Exec(s!, ct, "insert into inventory.unit_conversion(tenant_id,product_id,unit,factor) values (@t,@p,@u,@f) on conflict (tenant_id,product_id,unit) do update set factor=excluded.factor", ("t", t), ("p", m.ProductId), ("u", unit), ("f", m.Factor));
        await s!.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { productId = m.ProductId, unit, factor = m.Factor });
    }

    // ---------- B2 physical stock count ----------
    public async Task<ServiceResult<object>> CountSheetAsync(Guid uid, Guid locationId, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        var rows = await Rows(s!, @"select p.id, p.name, p.base_unit, coalesce(b.quantity,0) from catalog.product p
            left join inventory.stock_balance b on b.tenant_id=p.tenant_id and b.product_id=p.id and b.location_id=@l
            where p.is_active order by p.name", ct, ("l", locationId));
        return ServiceResult<object>.Ok(rows.Select(r => new { productId = r[0], product = r[1], unit = r[2], system = r[3] }).ToList());
    }

    public async Task<ServiceResult<object>> ApplyCountAsync(Guid uid, string? key, StockCountCommand m, CancellationToken ct)
    {
        if (key is null || key.Length is < 8 or > 90) return ServiceResult<object>.Fail("Idempotency-Key header (8-90 chars) required.");
        if (m.Lines is null || m.Lines.Count == 0) return ServiceResult<object>.Fail("At least one counted line is required.");
        if (m.Lines.Any(l => l.Counted < 0 || decimal.Round(l.Counted, 3) != l.Counted)) return ServiceResult<object>.Fail("Counted quantity must be zero or more with at most 3 decimals.");
        if (m.Lines.Select(l => l.ProductId).Distinct().Count() != m.Lines.Count) return ServiceResult<object>.Fail("A product appears more than once.");

        Dictionary<Guid, decimal> system;
        {
            var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
            await using var _s = s!;
            var rows = await Rows(s!, "select product_id, quantity from inventory.stock_balance where location_id=@l", ct, ("l", m.LocationId));
            system = rows.ToDictionary(r => (Guid)r[0], r => (decimal)r[1]);
        }

        var applied = new List<object>();
        var index = 0;
        foreach (var line in m.Lines)
        {
            index++;
            var diff = line.Counted - system.GetValueOrDefault(line.ProductId);
            if (diff == 0) continue;
            var cmd = new StockAdjustCommand(line.ProductId, m.LocationId, m.Date, Math.Abs(diff), diff > 0 ? "Increase" : "Decrease", diff > 0 ? "FoundOnCount" : "Shortage", "Physical stock count");
            var res = await finance.AdjustStockAsync(uid, $"{key}-{index}", cmd, ct);
            if (res.Error is not null) return ServiceResult<object>.Fail($"Line {index}: {res.Error}", res.Status);
            applied.Add(new { productId = line.ProductId, difference = diff });
        }
        return ServiceResult<object>.Ok(new { adjusted = applied.Count, lines = applied });
    }

    // ---------- B3 adjustment history ----------
    public async Task<ServiceResult<object>> AdjustmentHistoryAsync(Guid uid, Guid? productId, string? reason, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        var rows = await Rows(s!, @"select a.id, a.number, a.adjust_date, p.name, l.name, a.quantity_delta, a.value_delta, a.reason, a.note, a.created_at
            from inventory.stock_adjustment a
            join catalog.product p on p.tenant_id=a.tenant_id and p.id=a.product_id
            join core.stock_location l on l.tenant_id=a.tenant_id and l.id=a.location_id
            where (@p::uuid is null or a.product_id=@p) and (@r::text is null or a.reason=@r)
              and (@f::date is null or a.adjust_date>=@f) and (@e::date is null or a.adjust_date<=@e)
            order by a.adjust_date desc, a.number desc limit 500", ct,
            ("p", productId), ("r", string.IsNullOrWhiteSpace(reason) ? null : reason), ("f", from?.ToDateTime(TimeOnly.MinValue)), ("e", to?.ToDateTime(TimeOnly.MinValue)));
        return ServiceResult<object>.Ok(rows.Select(r => new { id = r[0], number = r[1], date = r[2], product = r[3], location = r[4], quantity = r[5], value = r[6], reason = r[7], note = r[8], createdAt = r[9] }).ToList());
    }

    // ---------- B4 low stock ----------
    public async Task<ServiceResult<object>> SetReorderLevelAsync(Guid uid, ReorderLevelCommand m, CancellationToken ct)
    {
        if (m.Level < 0 || decimal.Round(m.Level, 3) != m.Level) return ServiceResult<object>.Fail("Level must be zero or more with at most 3 decimals.");
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        if ((await Rows(s!, "select 1 from catalog.product where id=@p", ct, ("p", m.ProductId))).Count == 0) return ServiceResult<object>.Fail("Product not found.", 404);
        await Exec(s!, ct, "insert into inventory.reorder_level(tenant_id,product_id,level) values (@t,@p,@l) on conflict (tenant_id,product_id) do update set level=excluded.level, updated_at=now()", ("t", t), ("p", m.ProductId), ("l", m.Level));
        await s!.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { productId = m.ProductId, level = m.Level });
    }

    public async Task<ServiceResult<object>> LowStockAsync(Guid uid, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        var rows = await Rows(s!, @"select p.id, p.name, p.base_unit, r.level, coalesce(sum(b.quantity),0) as qty from inventory.reorder_level r
            join catalog.product p on p.tenant_id=r.tenant_id and p.id=r.product_id
            left join inventory.stock_balance b on b.tenant_id=r.tenant_id and b.product_id=r.product_id
            where p.is_active group by p.id, p.name, p.base_unit, r.level
            having coalesce(sum(b.quantity),0) <= r.level order by (r.level - coalesce(sum(b.quantity),0)) desc, p.name", ct);
        return ServiceResult<object>.Ok(rows.Select(r => new { productId = r[0], product = r[1], unit = r[2], reorderLevel = r[3], onHand = r[4] }).ToList());
    }

    // ---------- B5 dead stock ----------
    public async Task<ServiceResult<object>> DeadStockAsync(Guid uid, int days, DateOnly today, CancellationToken ct)
    {
        if (days is < 1 or > 3650) return ServiceResult<object>.Fail("Days must be between 1 and 3650.");
        var cutoff = today.AddDays(-days);
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        var rows = await Rows(s!, @"select p.id, p.name, p.base_unit, sum(b.quantity) as qty, coalesce(c.wac,0), sum(b.quantity)*coalesce(c.wac,0) as value, max(sl.sale_date) as last_sale
            from inventory.stock_balance b
            join catalog.product p on p.tenant_id=b.tenant_id and p.id=b.product_id
            left join inventory.item_cost c on c.tenant_id=b.tenant_id and c.product_id=b.product_id
            left join (select l.product_id, sa.sale_date from sales.sale_line l join sales.sale sa on sa.tenant_id=l.tenant_id and sa.id=l.sale_id) sl on sl.product_id=b.product_id
            where b.quantity > 0
            group by p.id, p.name, p.base_unit, c.wac
            having max(sl.sale_date) is null or max(sl.sale_date) < @c
            order by value desc, p.name", ct, ("c", cutoff.ToDateTime(TimeOnly.MinValue)));
        return ServiceResult<object>.Ok(rows.Select(r => new { productId = r[0], product = r[1], unit = r[2], onHand = r[3], wac = r[4], value = Math.Round((decimal)r[5], 2), lastSale = r[6] }).ToList());
    }

    // ---------- B6 stock ageing ----------
    public async Task<ServiceResult<object>> StockAgeingAsync(Guid uid, DateOnly today, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        var balances = await Rows(s!, @"select b.product_id, p.name, b.location_id, b.quantity, coalesce(c.wac,0) from inventory.stock_balance b
            join catalog.product p on p.tenant_id=b.tenant_id and p.id=b.product_id
            left join inventory.item_cost c on c.tenant_id=b.tenant_id and c.product_id=b.product_id
            where b.quantity > 0 order by p.name", ct);
        var movements = await Rows(s!, "select product_id, location_id, effective_date, quantity_delta from inventory.stock_movement where quantity_delta > 0 order by effective_date desc, occurred_at desc", ct);
        var byKey = movements.GroupBy(m => ((Guid)m[0], (Guid)m[1])).ToDictionary(g => g.Key, g => g.ToList());

        var items = new Dictionary<Guid, (string Name, decimal[] Qty, decimal Value)>();
        foreach (var b in balances)
        {
            var pid = (Guid)b[0]; var remaining = (decimal)b[3]; var wac = Convert.ToDecimal(b[4]);
            var qty = items.TryGetValue(pid, out var cur) ? cur.Qty : new decimal[Buckets.Length];
            // FIFO: oldest stock sells first, so what remains is the newest receipts.
            foreach (var m in byKey.GetValueOrDefault(((Guid)b[0], (Guid)b[2])) ?? [])
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, (decimal)m[3]);
                var age = Math.Max(0, today.DayNumber - ((DateOnly)m[2]).DayNumber);
                var idx = Array.FindIndex(Buckets, x => age >= x.From && age <= x.To);
                qty[idx] += take; remaining -= take;
            }
            if (remaining > 0) qty[Buckets.Length - 1] += remaining;
            items[pid] = ((string)b[1], qty, (cur.Value) + (decimal)b[3] * wac);
        }
        var result = items.Select(kv => new
        {
            productId = kv.Key, product = kv.Value.Name, value = Math.Round(kv.Value.Value, 2),
            buckets = Buckets.Select((x, i) => new { label = x.Label, quantity = kv.Value.Qty[i] }).ToList()
        }).OrderBy(x => x.product).ToList();
        return ServiceResult<object>.Ok(result);
    }
}
