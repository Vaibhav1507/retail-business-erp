using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Rbos.Api.Persistence;
using static Rbos.Api.Application.SqlHelpers;
using Rbos.Domain;

namespace Rbos.Api.Application;

public sealed record CollectionCommand(Guid CustomerId, DateOnly PaymentDate, decimal Amount, string? Method, string? Reference, IReadOnlyList<BillAllocation>? Allocations = null);
public sealed record SaleReturnCommand(Guid SaleId, DateOnly ReturnDate, string? Reason, List<ReturnLineInput>? Lines, string? RefundMethod = null);
public sealed record PurchaseReturnCommand(Guid ReceiptId, DateOnly ReturnDate, string? Reason, List<ReturnLineInput>? Lines);

/// <summary>Slice 5: customer collections, customer returns, supplier returns and print data. All amounts are recomputed server-side.</summary>
public sealed class AfterSalesService(TenantDb db)
{
    const string OpenSaleSql = @"select s.total - s.amount_paid - coalesce((select sum(a.amount) from sales.customer_payment_allocation a where a.sale_id=s.id),0) from sales.sale s where s.id=@b and s.customer_id=@p and not exists (select 1 from sales.sale_cancellation k where k.sale_id=s.id)";

    public async Task<ServiceResult<object>> OpenSalesAsync(Guid uid, Guid customerId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        var list = new List<object>();
        await using var c = s.Command(@"select x.id, x.sale_date, x.invoice_no, x.number, x.total, x.total - x.amount_paid - coalesce((select sum(a.amount) from sales.customer_payment_allocation a where a.sale_id=x.id),0) from sales.sale x where x.customer_id=@c and not exists (select 1 from sales.sale_cancellation k where k.sale_id=x.id) order by x.sale_date, x.created_at");
        c.Parameters.AddWithValue("c", customerId);
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            if (r.GetDecimal(5) > 0) list.Add(new { id = r.GetGuid(0), date = r.GetFieldValue<DateOnly>(1), invoiceNo = r.IsDBNull(2) ? null : r.GetString(2), number = r.GetInt64(3), total = r.GetDecimal(4), open = r.GetDecimal(5) });
        return ServiceResult<object>.Ok(list);
    }

    const string OwedSql = @"coalesce((select sum(total - amount_paid) from sales.sale where customer_id=@c and not exists (select 1 from sales.sale_cancellation k where k.sale_id=sales.sale.id)),0)
        - coalesce((select sum(amount) from sales.customer_payment where customer_id=@c),0)
        - coalesce((select sum(receivable_credit) from sales.sale_return where customer_id=@c),0)";

    // ---------- Collections ----------
    public async Task<ServiceResult<object>> CollectAsync(Guid uid, string? key, CollectionCommand m, string correlationId, CancellationToken ct)
    {
        key ??= "";
        if (key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        if ((m.Reference?.Trim().Length ?? 0) > 60) return ServiceResult<object>.Fail("Reference is too long.");
        var hash = Hash(string.Join('|', m.CustomerId, m.PaymentDate.ToString("O"), m.Amount.ToString(CultureInfo.InvariantCulture), m.Method, m.Reference?.Trim(), string.Join(',', (m.Allocations ?? []).Select(a => a.BillId.ToString() + ':' + a.Amount.ToString(CultureInfo.InvariantCulture)))));
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        var tenantId = tid.Value;
        try
        {
            await using var s = await db.BeginAsync(uid, tenantId, ct);
            var prior = await Prior(s, "sales.customer_payment", key, hash, ct);
            if (prior is not null) return prior;
            if (await Scalar<Guid?>(s, "select id from sales.customer where id=@i", ct, ("i", m.CustomerId)) is null) return ServiceResult<object>.Fail("Unknown customer.");
            var owed = await Scalar<decimal?>(s, "select " + OwedSql, ct, ("c", m.CustomerId)) ?? 0m;
            if (!AfterSalesRules.TryValidateCollection(m.Amount, m.Method, m.PaymentDate, DateOnly.FromDateTime(DateTime.UtcNow), owed, out var money, out var err))
                return ServiceResult<object>.Fail(err!);
            var journal = AfterSalesRules.CollectionJournal(money);
            var id = Guid.NewGuid(); var entry = Guid.NewGuid();
            await Exec(s, ct, "insert into audit.audit_event(tenant_id,id,actor_user_id,action,entity_type,entity_id,summary,correlation_id) values (@t,@a,@u,'CustomerPaymentReceived','CustomerPayment',@d,@s,@c)",
                ("t", tenantId), ("a", Guid.NewGuid()), ("u", uid), ("d", id), ("s", string.Create(CultureInfo.InvariantCulture, $"{m.Method} collection {money.Amount:0.00} INR")), ("c", correlationId));
            await PostJournal(s, tenantId, entry, m.PaymentDate, "CustomerPayment", id, journal, ct);
            await Exec(s, ct, "insert into sales.customer_payment(tenant_id,id,customer_id,payment_date,amount,method,reference,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@c,@d,@a,@m,@r,@k,@h,@j,@u)",
                ("t", tenantId), ("i", id), ("c", m.CustomerId), ("d", m.PaymentDate), ("a", money.Amount), ("m", m.Method), ("r", Blank(m.Reference)), ("k", key), ("h", hash), ("j", entry), ("u", uid));
            var allocErr = await AllocateAsync(s, tenantId, "sales.customer_payment_allocation", "sale_id", OpenSaleSql, m.CustomerId, id, money.Amount, m.Allocations, ct);
            if (allocErr is not null) return ServiceResult<object>.Fail(allocErr);
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id, owed = owed - money.Amount });
        }
        catch (PostgresException e) when (e.SqlState == "23505")
        {
            await using var s2 = await db.BeginAsync(uid, tenantId, ct);
            return await Prior(s2, "sales.customer_payment", key, hash, ct) ?? ServiceResult<object>.Fail("Duplicate payment.", 409);
        }
    }

    public async Task<ServiceResult<object>> PaymentsAsync(Guid uid, Guid? customerId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        await using var c = s.Command(@"select p.id, p.number, p.payment_date, c.name, p.amount, p.method, p.reference
            from sales.customer_payment p join sales.customer c on c.id=p.customer_id
            where (@c::uuid is null or p.customer_id=@c) order by p.payment_date desc, p.created_at desc limit 100");
        c.Parameters.AddWithValue("c", (object?)customerId ?? DBNull.Value);
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new { id = r.GetGuid(0), number = r.GetInt64(1), date = r.GetFieldValue<DateOnly>(2), customer = r.GetString(3), amount = r.GetDecimal(4), method = r.GetString(5), reference = r.IsDBNull(6) ? null : r.GetString(6) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> PaymentAsync(Guid uid, Guid id, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        object? head = null; Guid customerId = Guid.Empty;
        await using (var c = s.Command(@"select p.id, p.number, p.payment_date, c.name, p.amount, p.method, p.reference, p.customer_id
            from sales.customer_payment p join sales.customer c on c.id=p.customer_id where p.id=@i"))
        {
            c.Parameters.AddWithValue("i", id);
            await using var r = await c.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                customerId = r.GetGuid(7);
                head = new { id = r.GetGuid(0), number = r.GetInt64(1), date = r.GetFieldValue<DateOnly>(2), customer = r.GetString(3), amount = r.GetDecimal(4), method = r.GetString(5), reference = r.IsDBNull(6) ? null : r.GetString(6) };
            }
        }
        if (head is null) return ServiceResult<object>.Fail("Payment not found.", 404);
        var owed = await Scalar<decimal?>(s, "select " + OwedSql, ct, ("c", customerId)) ?? 0m;
        return ServiceResult<object>.Ok(new { payment = head, outstandingNow = owed });
    }

    public async Task<ServiceResult<object>> StatementAsync(Guid uid, Guid customerId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        var name = await Scalar<string>(s, "select name from sales.customer where id=@i", ct, ("i", customerId));
        if (name is null) return ServiceResult<object>.Fail("Customer not found.", 404);
        var rows = new List<(string Type, long Number, DateOnly Date, decimal Debit, decimal Credit, Guid Id)>();
        await using (var c = s.Command(@"select 'Sale' t, number, sale_date, created_at, total - amount_paid, 0::numeric, id
            from sales.sale where customer_id=@c and not exists (select 1 from sales.sale_cancellation k where k.sale_id=sales.sale.id)
            union all select 'Payment', number, payment_date, created_at, 0, amount, id from sales.customer_payment where customer_id=@c
            union all select 'Return', number, return_date, created_at, 0, receivable_credit, id from sales.sale_return where customer_id=@c and receivable_credit > 0
            order by 3, 4"))
        {
            c.Parameters.AddWithValue("c", customerId);
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) rows.Add((r.GetString(0), r.GetInt64(1), r.GetFieldValue<DateOnly>(2), r.GetDecimal(4), r.GetDecimal(5), r.GetGuid(6)));
        }
        decimal bal = 0; var entries = new List<object>();
        foreach (var x in rows) { bal += x.Debit - x.Credit; entries.Add(new { type = x.Type, number = x.Number, date = x.Date, debit = x.Debit, credit = x.Credit, balance = bal, id = x.Id }); }
        return ServiceResult<object>.Ok(new { customer = name, outstanding = bal, entries });
    }

    // ---------- Customer returns ----------
    public async Task<ServiceResult<object>> ReturnableSaleLinesAsync(Guid uid, Guid saleId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        var list = new List<object>();
        await using var c = s.Command(@"select l.product_id, p.name, l.quantity, l.line_total,
              coalesce((select sum(quantity) from sales.sale_return_line x where x.sale_id=l.sale_id and x.product_id=l.product_id),0)
            from sales.sale_line l join catalog.product p on p.id=l.product_id where l.sale_id=@s order by p.name");
        c.Parameters.AddWithValue("s", saleId);
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new { productId = r.GetGuid(0), product = r.GetString(1), sold = r.GetDecimal(2), lineTotal = r.GetDecimal(3), returned = r.GetDecimal(4), remaining = r.GetDecimal(2) - r.GetDecimal(4) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> ReturnSaleAsync(Guid uid, string? key, SaleReturnCommand m, string correlationId, CancellationToken ct)
    {
        key ??= "";
        if (key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        if (!AfterSalesRules.TryValidateReturnLines(m.Lines, m.Reason, out var lines, out var err)) return ServiceResult<object>.Fail(err!);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (m.ReturnDate > today) return ServiceResult<object>.Fail("Return date cannot be in the future.");
        if (m.ReturnDate < today.AddYears(-5)) return ServiceResult<object>.Fail("Return date is too old.");
        if (m.RefundMethod is not null && !SalesRules.PayMethods.Contains(m.RefundMethod)) return ServiceResult<object>.Fail("Refund method must be Cash, Bank or UPI.");
        var hash = Hash(string.Join('|', m.SaleId, m.ReturnDate.ToString("O"), m.Reason?.Trim(), m.RefundMethod, LinesKey(lines)));
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        var tenantId = tid.Value;
        try
        {
            await using var s = await db.BeginAsync(uid, tenantId, ct);
            var prior = await Prior(s, "sales.sale_return", key, hash, ct);
            if (prior is not null) return prior;

            Guid? customerId = null; Guid locationId = Guid.Empty; DateOnly saleDate = default; var found = false;
            await using (var q = s.Command("select customer_id, location_id, sale_date from sales.sale where id=@i"))
            {
                q.Parameters.AddWithValue("i", m.SaleId);
                await using var r = await q.ExecuteReaderAsync(ct);
                if (await r.ReadAsync(ct)) { found = true; customerId = r.IsDBNull(0) ? null : r.GetGuid(0); locationId = r.GetGuid(1); saleDate = r.GetFieldValue<DateOnly>(2); }
            }
            if (!found) return ServiceResult<object>.Fail("Sale not found.", 404);
            if (await Scalar<Guid?>(s, "select id from sales.sale_cancellation where sale_id=@i", ct, ("i", m.SaleId)) is not null) return ServiceResult<object>.Fail("This sale is cancelled and cannot be returned.", 409);
            if (m.ReturnDate < saleDate) return ServiceResult<object>.Fail("Return date cannot be before the sale date.");
            var costed = new List<(Guid ProductId, Guid InventoryProductId, decimal Qty, Money Amount, decimal UnitCost, Money Cost)>();
            foreach (var (productId, qty) in lines.OrderBy(x => x.ProductId))
            {
                decimal sold = 0, lineTotal = 0, unitCost = 0, retQty = 0, retAmt = 0; var has = false;
                await using (var q = s.Command(@"select l.quantity, l.line_total, l.unit_cost,
                      coalesce((select sum(quantity) from sales.sale_return_line x where x.sale_id=l.sale_id and x.product_id=l.product_id),0),
                      coalesce((select sum(amount) from sales.sale_return_line x where x.sale_id=l.sale_id and x.product_id=l.product_id),0)
                    from sales.sale_line l where l.sale_id=@s and l.product_id=@p"))
                {
                    q.Parameters.AddWithValue("s", m.SaleId); q.Parameters.AddWithValue("p", productId);
                    await using var r = await q.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct)) { has = true; sold = r.GetDecimal(0); lineTotal = r.GetDecimal(1); unitCost = r.GetDecimal(2); retQty = r.GetDecimal(3); retAmt = r.GetDecimal(4); }
                }
                if (!has) return ServiceResult<object>.Fail("A returned product was not on this sale.");
                if (qty.Value > sold - retQty) return ServiceResult<object>.Fail($"Cannot return {qty.Value:0.###}: only {sold - retQty:0.###} left to return for this product.");
                var amount = AfterSalesRules.ReturnedValue(lineTotal, sold, retQty, retAmt, qty.Value);
                var inventoryProductId = await CanonicalProductIdAsync(s, tenantId, productId, ct);
                costed.Add((productId, inventoryProductId, qty.Value, amount, unitCost, Money.Of(qty.Value * unitCost)));
            }
            var total = Money.Of(costed.Sum(x => x.Amount.Amount));
            var costTotal = Money.Of(costed.Sum(x => x.Cost.Amount));
            if (total.Amount <= 0) return ServiceResult<object>.Fail("Returned value is zero.");
            var credit = 0m;
            if (customerId is not null)
            {
                var owed = await Scalar<decimal?>(s, "select " + OwedSql, ct, ("c", customerId)) ?? 0m;
                credit = Math.Min(total.Amount, Math.Max(0m, owed));
            }
            var refund = total.Amount - credit;
            var journal = AfterSalesRules.CustomerReturnJournal(total, Money.Of(credit), Money.Of(refund), costTotal);

            var id = Guid.NewGuid(); var entry = Guid.NewGuid();
            await Exec(s, ct, "insert into audit.audit_event(tenant_id,id,actor_user_id,action,entity_type,entity_id,summary,correlation_id) values (@t,@a,@u,'SaleReturned','SaleReturn',@d,@s,@c)",
                ("t", tenantId), ("a", Guid.NewGuid()), ("u", uid), ("d", id), ("s", string.Create(CultureInfo.InvariantCulture, $"Customer return, {costed.Count} lines, value {total.Amount:0.00} INR")), ("c", correlationId));
            await PostJournal(s, tenantId, entry, m.ReturnDate, "SaleReturn", id, journal, ct);
            await Exec(s, ct, "insert into sales.sale_return(tenant_id,id,sale_id,customer_id,return_date,reason,total,receivable_credit,cash_refund,refund_method,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@s,@c,@d,@r,@v,@rc,@cr,@rm,@k,@h,@j,@u)",
                ("t", tenantId), ("i", id), ("s", m.SaleId), ("c", customerId), ("d", m.ReturnDate), ("r", Blank(m.Reason)), ("v", total.Amount), ("rc", credit), ("cr", refund), ("rm", refund > 0 ? m.RefundMethod : null), ("k", key), ("h", hash), ("j", entry), ("u", uid));
            foreach (var x in costed)
            {
                await Exec(s, ct, "insert into sales.sale_return_line(tenant_id,return_id,sale_id,product_id,quantity,amount,unit_cost) values (@t,@r,@s,@p,@q,@a,@c)",
                    ("t", tenantId), ("r", id), ("s", m.SaleId), ("p", x.ProductId), ("q", x.Qty), ("a", x.Amount.Amount), ("c", x.UnitCost));
                if (x.Cost.Amount > 0)
                    await Exec(s, ct, "insert into inventory.stock_movement(tenant_id,id,product_id,location_id,quantity_delta,unit_cost,value_delta,effective_date,source_type,source_id) values (@t,@i,@p,@l,@q,@c,@v,@e,'SaleReturn',@d)",
                        ("t", tenantId), ("i", Guid.NewGuid()), ("p", x.InventoryProductId), ("l", locationId), ("q", x.Qty), ("c", x.UnitCost), ("v", x.Cost.Amount), ("e", m.ReturnDate), ("d", id));
                await Exec(s, ct, "insert into inventory.stock_balance(tenant_id,product_id,location_id,quantity) values (@t,@p,@l,@q) on conflict (tenant_id,product_id,location_id) do update set quantity = inventory.stock_balance.quantity + excluded.quantity",
                    ("t", tenantId), ("p", x.ProductId), ("l", locationId), ("q", x.Qty));
                var cur = await ReadCostAsync(s, x.ProductId, ct);
                var tq = cur.TotalQuantity + x.Qty; var tv = cur.InventoryValue + x.Cost.Amount;
                await Exec(s, ct, "insert into inventory.item_cost(tenant_id,product_id,total_quantity,inventory_value,wac) values (@t,@p,@q,@v,@w) on conflict (tenant_id,product_id) do update set total_quantity=excluded.total_quantity, inventory_value=excluded.inventory_value, wac=excluded.wac",
                    ("t", tenantId), ("p", x.InventoryProductId), ("q", tq), ("v", tv), ("w", decimal.Round(tv / tq, 4, MidpointRounding.AwayFromZero)));
            }
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id, total = total.Amount, balanceCredit = credit, cashRefund = refund });
        }
        catch (PostgresException e) when (e.SqlState == "23505")
        {
            await using var s2 = await db.BeginAsync(uid, tenantId, ct);
            return await Prior(s2, "sales.sale_return", key, hash, ct) ?? ServiceResult<object>.Fail("Duplicate return.", 409);
        }
    }

    public async Task<ServiceResult<object>> SaleReturnsAsync(Guid uid, string? q, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        await using var c = s.Command(@"select r.id, r.number, r.return_date, sl.number, coalesce(c.name,'Walk-in'), r.total, r.receivable_credit, r.cash_refund, r.reason, r.refund_method
            from sales.sale_return r join sales.sale sl on sl.id=r.sale_id left join sales.customer c on c.id=r.customer_id
            where (@q::text is null or lower(coalesce(c.name,'walk-in')) like '%' || lower(@q) || '%' or r.number::text=@q or sl.number::text=@q or lower(coalesce(r.reason,'')) like '%' || lower(@q) || '%')
            order by r.return_date desc, r.created_at desc limit 100");
        c.Parameters.AddWithValue("q", (object?)Blank(q) ?? DBNull.Value);
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new { id = r.GetGuid(0), number = r.GetInt64(1), date = r.GetFieldValue<DateOnly>(2), saleNumber = r.GetInt64(3), customer = r.GetString(4), total = r.GetDecimal(5), balanceCredit = r.GetDecimal(6), cashRefund = r.GetDecimal(7), reason = r.IsDBNull(8) ? null : r.GetString(8), refundMethod = r.IsDBNull(9) ? null : r.GetString(9) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> SaleReturnAsync(Guid uid, Guid id, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        object? head = null;
        await using (var c = s.Command(@"select r.id, r.number, r.return_date, sl.number, coalesce(c.name,'Walk-in'), r.total, r.receivable_credit, r.cash_refund, r.reason
            from sales.sale_return r join sales.sale sl on sl.id=r.sale_id left join sales.customer c on c.id=r.customer_id where r.id=@i"))
        {
            c.Parameters.AddWithValue("i", id);
            await using var r = await c.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
                head = new { id = r.GetGuid(0), number = r.GetInt64(1), date = r.GetFieldValue<DateOnly>(2), saleNumber = r.GetInt64(3), customer = r.GetString(4), total = r.GetDecimal(5), balanceCredit = r.GetDecimal(6), cashRefund = r.GetDecimal(7), reason = r.IsDBNull(8) ? null : r.GetString(8) };
        }
        if (head is null) return ServiceResult<object>.Fail("Return not found.", 404);
        var lines = new List<object>();
        await using (var c = s.Command("select p.name, p.internal_sku, l.quantity, l.amount from sales.sale_return_line l join catalog.product p on p.id=l.product_id where l.return_id=@i order by p.name"))
        {
            c.Parameters.AddWithValue("i", id);
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) lines.Add(new { product = r.GetString(0), sku = r.GetString(1), quantity = r.GetDecimal(2), amount = r.GetDecimal(3) });
        }
        return ServiceResult<object>.Ok(new { @return = head, lines });
    }

    // ---------- Supplier returns ----------
    public async Task<ServiceResult<object>> PurchaseAsync(Guid uid, Guid id, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        object? head = null;
        await using (var c = s.Command(@"select r.id, r.receipt_date, sp.name, r.supplier_ref, r.total, l.name, r.number
            from procurement.purchase_receipt r join procurement.supplier sp on sp.id=r.supplier_id join core.stock_location l on l.id=r.location_id where r.id=@i"))
        {
            c.Parameters.AddWithValue("i", id);
            await using var r = await c.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
                head = new { id = r.GetGuid(0), date = r.GetFieldValue<DateOnly>(1), supplier = r.GetString(2), supplierRef = r.IsDBNull(3) ? null : r.GetString(3), total = r.GetDecimal(4), location = r.GetString(5), number = r.GetInt64(6) };
        }
        if (head is null) return ServiceResult<object>.Fail("Receipt not found.", 404);
        var lines = new List<object>();
        await using (var c = s.Command(@"select l.product_id, p.name, p.internal_sku, l.quantity, l.unit_cost,
              coalesce((select sum(quantity) from procurement.purchase_return_line x where x.receipt_id=l.receipt_id and x.product_id=l.product_id),0)
            from procurement.purchase_receipt_line l join catalog.product p on p.id=l.product_id where l.receipt_id=@i order by p.name"))
        {
            c.Parameters.AddWithValue("i", id);
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                lines.Add(new { productId = r.GetGuid(0), product = r.GetString(1), sku = r.GetString(2), quantity = r.GetDecimal(3), unitCost = r.GetDecimal(4), lineTotal = Money.Of(r.GetDecimal(3) * r.GetDecimal(4)).Amount, returned = r.GetDecimal(5), remaining = r.GetDecimal(3) - r.GetDecimal(5) });
        }
        return ServiceResult<object>.Ok(new { receipt = head, lines });
    }

    public async Task<ServiceResult<object>> ReturnPurchaseAsync(Guid uid, string? key, PurchaseReturnCommand m, string correlationId, CancellationToken ct)
    {
        key ??= "";
        if (key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        if (!AfterSalesRules.TryValidateReturnLines(m.Lines, m.Reason, out var lines, out var err)) return ServiceResult<object>.Fail(err!);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (m.ReturnDate > today) return ServiceResult<object>.Fail("Return date cannot be in the future.");
        if (m.ReturnDate < today.AddYears(-5)) return ServiceResult<object>.Fail("Return date is too old.");
        var hash = Hash(string.Join('|', m.ReceiptId, m.ReturnDate.ToString("O"), m.Reason?.Trim(), LinesKey(lines)));
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        var tenantId = tid.Value;
        try
        {
            await using var s = await db.BeginAsync(uid, tenantId, ct);
            var prior = await Prior(s, "procurement.purchase_return", key, hash, ct);
            if (prior is not null) return prior;

            Guid supplierId = Guid.Empty, locationId = Guid.Empty; DateOnly receiptDate = default; var found = false; var gstClaimed = false;
            await using (var q = s.Command("select supplier_id, location_id, receipt_date, gst_claimed from procurement.purchase_receipt where id=@i"))
            {
                q.Parameters.AddWithValue("i", m.ReceiptId);
                await using var r = await q.ExecuteReaderAsync(ct);
                if (await r.ReadAsync(ct)) { found = true; supplierId = r.GetGuid(0); locationId = r.GetGuid(1); receiptDate = r.GetFieldValue<DateOnly>(2); gstClaimed = r.GetBoolean(3); }
            }
            if (!found) return ServiceResult<object>.Fail("Purchase receipt not found.", 404);
            if (m.ReturnDate < receiptDate) return ServiceResult<object>.Fail("Return date cannot be before the receipt date.");

            var costed = new List<(Guid ProductId, Guid InventoryProductId, decimal Qty, Money Amount, decimal UnitCost, Money Removed, Money Gst)>();
            foreach (var (productId, qty) in lines.OrderBy(x => x.ProductId))
            {
                decimal bought = 0, unitCost = 0, retQty = 0, retAmt = 0, lineGst = 0; var has = false;
                await using (var q = s.Command(@"select l.quantity, l.unit_cost, l.gst_amount,
                      coalesce((select sum(quantity) from procurement.purchase_return_line x where x.receipt_id=l.receipt_id and x.product_id=l.product_id),0),
                      coalesce((select sum(amount) from procurement.purchase_return_line x where x.receipt_id=l.receipt_id and x.product_id=l.product_id),0)
                    from procurement.purchase_receipt_line l where l.receipt_id=@s and l.product_id=@p"))
                {
                    q.Parameters.AddWithValue("s", m.ReceiptId); q.Parameters.AddWithValue("p", productId);
                    await using var r = await q.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct)) { has = true; bought = r.GetDecimal(0); unitCost = r.GetDecimal(1); lineGst = r.GetDecimal(2); retQty = r.GetDecimal(3); retAmt = r.GetDecimal(4); }
                }
                if (!has) return ServiceResult<object>.Fail("A returned product was not on this receipt.");
                if (qty.Value > bought - retQty) return ServiceResult<object>.Fail($"Cannot return {qty.Value:0.###}: only {bought - retQty:0.###} left to return for this product.");
                var inventoryProductId = await CanonicalProductIdAsync(s, tenantId, productId, ct);
                var onHand = await Scalar<decimal?>(s, "select quantity from inventory.stock_balance where product_id=@p and location_id=@l for update", ct, ("p", inventoryProductId), ("l", locationId)) ?? 0m;
                if (onHand < qty.Value)
                {
                    var name = await Scalar<string>(s, "select name from catalog.product where id=@p", ct, ("p", productId));
                    return ServiceResult<object>.Fail($"Not enough stock to return {name}: {onHand:0.###} on hand.");
                }
                var cur = await ReadCostAsync(s, inventoryProductId, ct);
                var remaining = cur.TotalQuantity - qty.Value;
                var removed = remaining <= 0 ? cur.InventoryValue : Math.Min(cur.InventoryValue, Money.Of(qty.Value * cur.Wac).Amount);
                var billed = Money.Of(bought * unitCost + lineGst).Amount; var amount = AfterSalesRules.ReturnedValue(billed, bought, retQty, retAmt, qty.Value); var gstPart = gstClaimed && billed > 0 ? Money.Of(Math.Min(amount.Amount, Math.Round(lineGst * amount.Amount / billed, 2))) : Money.Of(0m);
                costed.Add((productId, inventoryProductId, qty.Value, amount, unitCost, Money.Of(removed), gstPart));
            }
            var total = Money.Of(costed.Sum(x => x.Amount.Amount));
            var removedTotal = Money.Of(costed.Sum(x => x.Removed.Amount));
            if (total.Amount <= 0) return ServiceResult<object>.Fail("Returned value is zero.");
            var gstReversed = Money.Of(costed.Sum(x => x.Gst.Amount)); var journal = AfterSalesRules.SupplierReturnJournal(total, removedTotal, gstReversed);

            var id = Guid.NewGuid(); var entry = Guid.NewGuid();
            await Exec(s, ct, "insert into audit.audit_event(tenant_id,id,actor_user_id,action,entity_type,entity_id,summary,correlation_id) values (@t,@a,@u,'PurchaseReturned','PurchaseReturn',@d,@s,@c)",
                ("t", tenantId), ("a", Guid.NewGuid()), ("u", uid), ("d", id), ("s", string.Create(CultureInfo.InvariantCulture, $"Supplier return, {costed.Count} lines, credit {total.Amount:0.00} INR")), ("c", correlationId));
            await PostJournal(s, tenantId, entry, m.ReturnDate, "PurchaseReturn", id, journal, ct);
            await Exec(s, ct, "insert into procurement.purchase_return(tenant_id,id,receipt_id,supplier_id,location_id,return_date,reason,total,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@r,@s,@l,@d,@n,@v,@k,@h,@j,@u)",
                ("t", tenantId), ("i", id), ("r", m.ReceiptId), ("s", supplierId), ("l", locationId), ("d", m.ReturnDate), ("n", Blank(m.Reason)), ("v", total.Amount), ("k", key), ("h", hash), ("j", entry), ("u", uid));
            foreach (var x in costed)
            {
                await Exec(s, ct, "insert into procurement.purchase_return_line(tenant_id,return_id,receipt_id,product_id,quantity,unit_cost,amount) values (@t,@r,@s,@p,@q,@c,@a)",
                    ("t", tenantId), ("r", id), ("s", m.ReceiptId), ("p", x.ProductId), ("q", x.Qty), ("c", x.UnitCost), ("a", x.Amount.Amount));
                var cur = await ReadCostAsync(s, x.ProductId, ct);
                var remaining = cur.TotalQuantity - x.Qty;
                await Exec(s, ct, "insert into inventory.stock_movement(tenant_id,id,product_id,location_id,quantity_delta,unit_cost,value_delta,effective_date,source_type,source_id) values (@t,@i,@p,@l,@q,@c,@v,@e,'PurchaseReturn',@d)",
                    ("t", tenantId), ("i", Guid.NewGuid()), ("p", x.InventoryProductId), ("l", locationId), ("q", -x.Qty), ("c", cur.Wac), ("v", -x.Removed.Amount), ("e", m.ReturnDate), ("d", id));
                await Exec(s, ct, "update inventory.stock_balance set quantity = quantity - @q where product_id=@p and location_id=@l", ("q", x.Qty), ("p", x.ProductId), ("l", locationId));
                await Exec(s, ct, "update inventory.item_cost set total_quantity=@q, inventory_value=@v, wac=@w where product_id=@p",
                    ("q", remaining), ("v", cur.InventoryValue - x.Removed.Amount), ("w", cur.Wac), ("p", x.ProductId));
            }
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id, total = total.Amount });
        }
        catch (PostgresException e) when (e.SqlState == "23505")
        {
            await using var s2 = await db.BeginAsync(uid, tenantId, ct);
            return await Prior(s2, "procurement.purchase_return", key, hash, ct) ?? ServiceResult<object>.Fail("Duplicate return.", 409);
        }
    }

    public async Task<ServiceResult<object>> PurchaseReturnsAsync(Guid uid, string? q, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        await using var c = s.Command(@"select r.id, r.number, r.return_date, sp.name, pr.number, r.total, r.reason
            from procurement.purchase_return r join procurement.supplier sp on sp.id=r.supplier_id join procurement.purchase_receipt pr on pr.id=r.receipt_id
            where (@q::text is null or lower(sp.name) like '%' || lower(@q) || '%' or r.number::text=@q or pr.number::text=@q or lower(coalesce(r.reason,'')) like '%' || lower(@q) || '%')
            order by r.return_date desc, r.created_at desc limit 100");
        c.Parameters.AddWithValue("q", (object?)Blank(q) ?? DBNull.Value);
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new { id = r.GetGuid(0), number = r.GetInt64(1), date = r.GetFieldValue<DateOnly>(2), supplier = r.GetString(3), receiptNumber = r.GetInt64(4), total = r.GetDecimal(5), reason = r.IsDBNull(6) ? null : r.GetString(6) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> PurchaseReturnAsync(Guid uid, Guid id, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        object? head = null;
        await using (var c = s.Command(@"select r.id, r.number, r.return_date, sp.name, pr.number, r.total, r.reason
            from procurement.purchase_return r join procurement.supplier sp on sp.id=r.supplier_id join procurement.purchase_receipt pr on pr.id=r.receipt_id where r.id=@i"))
        {
            c.Parameters.AddWithValue("i", id);
            await using var r = await c.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
                head = new { id = r.GetGuid(0), number = r.GetInt64(1), date = r.GetFieldValue<DateOnly>(2), supplier = r.GetString(3), receiptNumber = r.GetInt64(4), total = r.GetDecimal(5), reason = r.IsDBNull(6) ? null : r.GetString(6) };
        }
        if (head is null) return ServiceResult<object>.Fail("Return not found.", 404);
        var lines = new List<object>();
        await using (var c = s.Command("select p.name, p.internal_sku, l.quantity, l.unit_cost, l.amount from procurement.purchase_return_line l join catalog.product p on p.id=l.product_id where l.return_id=@i order by p.name"))
        {
            c.Parameters.AddWithValue("i", id);
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) lines.Add(new { product = r.GetString(0), sku = r.GetString(1), quantity = r.GetDecimal(2), unitCost = r.GetDecimal(3), amount = r.GetDecimal(4) });
        }
        return ServiceResult<object>.Ok(new { @return = head, lines });
    }

    public async Task<ServiceResult<object>> BusinessAsync(Guid uid, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        var name = await Scalar<string>(s, "select display_name from core.business_profile where tenant_id=@t", ct, ("t", tid.Value));
        return ServiceResult<object>.Ok(new { name = name ?? "" });
    }

    // ---------- Sale cancellation (A2) ----------
    public async Task<ServiceResult<object>> CancelSaleAsync(Guid uid, string? key, Guid saleId, string? reason, string correlationId, CancellationToken ct)
    {
        key ??= "";
        if (key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        if ((reason?.Trim().Length ?? 0) > 200) return ServiceResult<object>.Fail("Reason is too long.");
        var hash = Hash(string.Join('|', saleId, reason?.Trim()));
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        var tenantId = tid.Value;
        try
        {
            await using var s = await db.BeginAsync(uid, tenantId, ct);
            var prior = await Prior(s, "sales.sale_cancellation", key, hash, ct);
            if (prior is not null) return prior;

            Guid locationId = Guid.Empty; DateOnly saleDate = default; decimal total = 0, paid = 0, costTotal = 0; var found = false;
            await using (var q = s.Command("select location_id, sale_date, total, amount_paid, cost_total from sales.sale where id=@i"))
            {
                q.Parameters.AddWithValue("i", saleId);
                await using var r = await q.ExecuteReaderAsync(ct);
                if (await r.ReadAsync(ct)) { found = true; locationId = r.GetGuid(0); saleDate = r.GetFieldValue<DateOnly>(1); total = r.GetDecimal(2); paid = r.GetDecimal(3); costTotal = r.GetDecimal(4); }
            }
            if (!found) return ServiceResult<object>.Fail("Sale not found.", 404);
            if (await Scalar<Guid?>(s, "select id from sales.sale_cancellation where sale_id=@i", ct, ("i", saleId)) is not null) return ServiceResult<object>.Fail("This sale is already cancelled.", 409);
            if (await Scalar<Guid?>(s, "select id from sales.sale_return where sale_id=@i limit 1", ct, ("i", saleId)) is not null) return ServiceResult<object>.Fail("This sale has returns against it and cannot be cancelled.", 409);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var cancelDate = today < saleDate ? saleDate : today;
            var journal = SalesRules.SaleJournal(Money.Of(total), Money.Of(paid), Money.Of(costTotal))
                .Select(j => new JournalLineDraft(j.AccountCode, j.Credit, j.Debit)).ToList();

            var historicalLines = new List<(Guid ProductId, decimal Qty, decimal Cost)>();
            await using (var q = s.Command(@"select l.product_id, l.quantity, coalesce((select -sum(m.value_delta) from inventory.stock_movement m where m.source_type='Sale' and m.source_id=l.sale_id and m.product_id=l.product_id),0)
                from sales.sale_line l where l.sale_id=@i order by l.product_id"))
            {
                q.Parameters.AddWithValue("i", saleId);
                await using var r = await q.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) historicalLines.Add((r.GetGuid(0), r.GetDecimal(1), r.GetDecimal(2)));
            }

            var lines = new List<(Guid ProductId, Guid InventoryProductId, decimal Qty, decimal Cost)>();
            foreach (var line in historicalLines)
                lines.Add((line.ProductId, await CanonicalProductIdAsync(s, tenantId, line.ProductId, ct), line.Qty, line.Cost));

            var id = Guid.NewGuid(); var entry = Guid.NewGuid();
            await Exec(s, ct, "insert into audit.audit_event(tenant_id,id,actor_user_id,action,entity_type,entity_id,summary,correlation_id) values (@t,@a,@u,'SaleCancelled','Sale',@d,@s,@c)",
                ("t", tenantId), ("a", Guid.NewGuid()), ("u", uid), ("d", saleId), ("s", string.Create(CultureInfo.InvariantCulture, $"Sale cancelled, value {total:0.00} INR")), ("c", correlationId));
            await PostJournal(s, tenantId, entry, cancelDate, "SaleCancellation", id, journal, ct);
            await Exec(s, ct, "insert into sales.sale_cancellation(tenant_id,id,sale_id,cancel_date,reason,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@s,@d,@r,@k,@h,@j,@u)",
                ("t", tenantId), ("i", id), ("s", saleId), ("d", cancelDate), ("r", Blank(reason)), ("k", key), ("h", hash), ("j", entry), ("u", uid));
            foreach (var x in lines)
            {
                var unitCost = x.Qty == 0 ? 0m : decimal.Round(x.Cost / x.Qty, 4, MidpointRounding.AwayFromZero);
                if (x.Cost > 0)
                    await Exec(s, ct, "insert into inventory.stock_movement(tenant_id,id,product_id,location_id,quantity_delta,unit_cost,value_delta,effective_date,source_type,source_id) values (@t,@i,@p,@l,@q,@c,@v,@e,'SaleCancellation',@d)",
                        ("t", tenantId), ("i", Guid.NewGuid()), ("p", x.InventoryProductId), ("l", locationId), ("q", x.Qty), ("c", unitCost), ("v", x.Cost), ("e", cancelDate), ("d", id));
                await Exec(s, ct, "insert into inventory.stock_balance(tenant_id,product_id,location_id,quantity) values (@t,@p,@l,@q) on conflict (tenant_id,product_id,location_id) do update set quantity = inventory.stock_balance.quantity + excluded.quantity",
                    ("t", tenantId), ("p", x.ProductId), ("l", locationId), ("q", x.Qty));
                var cur = await ReadCostAsync(s, x.ProductId, ct);
                var tq = cur.TotalQuantity + x.Qty; var tv = cur.InventoryValue + x.Cost;
                await Exec(s, ct, "insert into inventory.item_cost(tenant_id,product_id,total_quantity,inventory_value,wac) values (@t,@p,@q,@v,@w) on conflict (tenant_id,product_id) do update set total_quantity=excluded.total_quantity, inventory_value=excluded.inventory_value, wac=excluded.wac",
                    ("t", tenantId), ("p", x.InventoryProductId), ("q", tq), ("v", tv), ("w", decimal.Round(tv / tq, 4, MidpointRounding.AwayFromZero)));
            }
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id, saleId, cancelled = true });
        }
        catch (PostgresException e) when (e.SqlState == "23505")
        {
            await using var s2 = await db.BeginAsync(uid, tenantId, ct);
            return await Prior(s2, "sales.sale_cancellation", key, hash, ct) ?? ServiceResult<object>.Fail("This sale is already cancelled.", 409);
        }
        catch (PostgresException e) when (e.SqlState == "RB001") { return ServiceResult<object>.Fail(e.MessageText, 409); }
    }

    // ---------- helpers ----------
    static string LinesKey(IReadOnlyList<(Guid ProductId, Quantity Quantity)> lines)
        => string.Join(';', lines.OrderBy(l => l.ProductId).Select(l => string.Create(CultureInfo.InvariantCulture, $"{l.ProductId}:{l.Quantity.Value}")));

    static async Task PostJournal(TenantSession s, Guid tenantId, Guid entry, DateOnly date, string sourceType, Guid sourceId, IReadOnlyList<JournalLineDraft> journal, CancellationToken ct)
    {
        await Exec(s, ct, "insert into accounting.journal_entry(tenant_id,id,effective_date,source_type,source_id) values (@t,@e,@d,@y,@s)", ("t", tenantId), ("e", entry), ("d", date), ("y", sourceType), ("s", sourceId));
        foreach (var j in journal)
        {
            var account = await AccountAsync(s, tenantId, j.AccountCode, ct);
            await Exec(s, ct, "insert into accounting.journal_line(tenant_id,id,entry_id,account_id,debit,credit) values (@t,@i,@e,@a,@d,@c)",
                ("t", tenantId), ("i", Guid.NewGuid()), ("e", entry), ("a", account), ("d", j.Debit), ("c", j.Credit));
        }
    }

    static async Task<Guid> AccountAsync(TenantSession s, Guid tenantId, string code, CancellationToken ct)
    {
        var (name, type) = code switch
        {
            "1000" => ("Cash and Bank", "Asset"), "1200" => ("Accounts Receivable", "Asset"), "1400" => ("Inventory", "Asset"), "2000" => ("Accounts Payable", "Liability"),
            "4000" => ("Sales Revenue", "Income"), "5000" => ("Cost of Goods Sold", "Expense"), _ => (code, "Asset")
        };
        await Exec(s, ct, "insert into accounting.account(tenant_id,id,code,name,type) values (@t,@i,@c,@n,@y) on conflict (tenant_id,code) do nothing",
            ("t", tenantId), ("i", Guid.NewGuid()), ("c", code), ("n", name), ("y", type));
        return (await Scalar<Guid?>(s, "select id from accounting.account where code=@c", ct, ("c", code)))!.Value;
    }

    static async Task<Guid> CanonicalProductIdAsync(TenantSession s, Guid tenantId, Guid productId, CancellationToken ct)
    {
        return await Scalar<Guid?>(s, @"select coalesce(
            (select canonical_product_id from catalog.product_alias where tenant_id=@t and alias_product_id=@p),
            @p)", ct, ("t", tenantId), ("p", productId)) ?? productId;
    }

    static async Task<CostState> ReadCostAsync(TenantSession s, Guid productId, CancellationToken ct)
    {
        await using var q = s.Command("select total_quantity,inventory_value,wac from inventory.item_cost where product_id=@p for update");
        q.Parameters.AddWithValue("p", productId);
        await using var r = await q.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new CostState(r.GetDecimal(0), r.GetDecimal(1), r.GetDecimal(2)) : CostState.Empty;
    }

    }
