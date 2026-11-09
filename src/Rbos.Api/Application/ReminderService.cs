using Rbos.Api.Persistence;
using static Rbos.Api.Application.SqlHelpers;

namespace Rbos.Api.Application;

/// <summary>G5: read-only, in-app reminders derived from existing data. No scheduler or outbound messages.</summary>
public sealed class ReminderService(TenantDb db)
{
    public async Task<ServiceResult<object>> ListAsync(Guid uid, DateOnly today, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);

        var overdueOrders = await Scalar<long?>(s, "select count(*) from procurement.purchase_order where status='Ordered' and expected_date is not null and expected_date < @d", ct, ("d", today)) ?? 0;
        var low = await Scalar<long?>(s, @"select count(*) from (select r.product_id from inventory.reorder_level r
            join catalog.product p on p.tenant_id=r.tenant_id and p.id=r.product_id
            left join inventory.stock_balance b on b.tenant_id=r.tenant_id and b.product_id=r.product_id
            where p.is_active group by r.product_id, r.level having coalesce(sum(b.quantity),0) <= r.level) t", ct) ?? 0;
        var unclosedDays = await Scalar<long?>(s, @"select count(*) from (select distinct x.sale_date from sales.sale x
            where x.sale_date < @d and x.sale_date >= @d - 7
              and not exists (select 1 from accounting.cash_closing c where c.closing_date = x.sale_date)) t", ct, ("d", today)) ?? 0;
        var collect = await Scalar<decimal?>(s, @"select
            coalesce((select sum(total - amount_paid) from sales.sale x where not exists (select 1 from sales.sale_cancellation k where k.sale_id=x.id)),0)
            - coalesce((select sum(amount) from sales.customer_payment),0)
            - coalesce((select sum(receivable_credit) from sales.sale_return),0)", ct) ?? 0m;
        var pay = await Scalar<decimal?>(s, @"select
            coalesce((select sum(total) from procurement.purchase_receipt),0)
            - coalesce((select sum(amount) from procurement.supplier_payment),0)
            - coalesce((select sum(total) from procurement.purchase_return),0)", ct) ?? 0m;

        var items = new List<object>();
        if (overdueOrders > 0) items.Add(new { kind = "orders", severity = "warn", count = overdueOrders, amount = (decimal?)null });
        if (low > 0) items.Add(new { kind = "lowStock", severity = "warn", count = low, amount = (decimal?)null });
        if (unclosedDays > 0) items.Add(new { kind = "dayClosing", severity = "info", count = unclosedDays, amount = (decimal?)null });
        if (collect > 0) items.Add(new { kind = "collect", severity = "info", count = (long?)null, amount = (decimal?)collect });
        if (pay > 0) items.Add(new { kind = "pay", severity = "info", count = (long?)null, amount = (decimal?)pay });
        return ServiceResult<object>.Ok(items);
    }
}
