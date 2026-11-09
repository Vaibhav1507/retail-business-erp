using Rbos.Api.Persistence;
using static Rbos.Api.Application.SqlHelpers;

namespace Rbos.Api.Application;

/// <summary>F3: one read-only summary for the home dashboard.</summary>
public sealed class DashboardService(TenantDb db)
{
    public async Task<ServiceResult<object>> SummaryAsync(Guid uid, DateOnly today, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);

        const string live = "not exists (select 1 from sales.sale_cancellation k where k.sale_id=x.id)";
        var billsToday = await Scalar<long?>(s, "select count(*) from sales.sale x where x.sale_date=@d and " + live, ct, ("d", today)) ?? 0;
        var totalToday = await Scalar<decimal?>(s, "select coalesce(sum(x.total),0) from sales.sale x where x.sale_date=@d and " + live, ct, ("d", today)) ?? 0m;
        var collect = await Scalar<decimal?>(s, @"select
            coalesce((select sum(total - amount_paid) from sales.sale x where not exists (select 1 from sales.sale_cancellation k where k.sale_id=x.id)),0)
            - coalesce((select sum(amount) from sales.customer_payment),0)
            - coalesce((select sum(receivable_credit) from sales.sale_return),0)", ct) ?? 0m;
        var pay = await Scalar<decimal?>(s, @"select
            coalesce((select sum(total) from procurement.purchase_receipt),0)
            - coalesce((select sum(amount) from procurement.supplier_payment),0)
            - coalesce((select sum(total) from procurement.purchase_return),0)", ct) ?? 0m;
        var low = await Scalar<long?>(s, @"select count(*) from (select r.product_id from inventory.reorder_level r
            join catalog.product p on p.tenant_id=r.tenant_id and p.id=r.product_id
            left join inventory.stock_balance b on b.tenant_id=r.tenant_id and b.product_id=r.product_id
            where p.is_active group by r.product_id, r.level having coalesce(sum(b.quantity),0) <= r.level) t", ct) ?? 0;
        var money = await Scalar<decimal?>(s, @"select coalesce(sum(l.debit-l.credit),0) from accounting.money_account m
            join accounting.journal_line l on l.account_id=m.account_id", ct) ?? 0m;

        return ServiceResult<object>.Ok(new
        {
            date = today,
            salesToday = new { bills = billsToday, total = totalToday },
            moneyToCollect = collect,
            moneyToPay = pay,
            lowStockCount = low,
            cashAndBank = money
        });
    }
}

