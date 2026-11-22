using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Rbos.Api.Persistence;
using static Rbos.Api.Application.SqlHelpers;
using Rbos.Domain;

namespace Rbos.Api.Application;

public sealed record SupplierCommand(string? Name, string? Phone, string? Notes, bool IsActive = true);
public sealed record ReceiptCommand(Guid SupplierId, Guid LocationId, DateOnly ReceiptDate, string? SupplierRef, List<PurchaseLineInput>? Lines, Guid? InvoiceFileId = null);
public sealed record PaymentCommand(Guid SupplierId, DateOnly PaymentDate, decimal Amount, string? Method, string? Reference, IReadOnlyList<BillAllocation>? Allocations = null);

/// <summary>Suppliers, purchase receipts (stock + WAC + payable) and supplier payments. All amounts are recomputed server-side.</summary>
public sealed class ProcurementService(TenantDb db)
{
    const string OpenReceiptSql = @"select r.total - coalesce((select sum(a.amount) from procurement.supplier_payment_allocation a where a.receipt_id=r.id),0) from procurement.purchase_receipt r where r.id=@b and r.supplier_id=@p";

    public async Task<ServiceResult<object>> OpenBillsAsync(Guid uid, Guid supplierId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        var list = new List<object>();
        await using var c = s.Command(@"select r.id, r.receipt_date, r.supplier_ref, r.total, r.total - coalesce((select sum(a.amount) from procurement.supplier_payment_allocation a where a.receipt_id=r.id),0) from procurement.purchase_receipt r where r.supplier_id=@s order by r.receipt_date, r.created_at");
        c.Parameters.AddWithValue("s", supplierId);
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            if (r.GetDecimal(4) > 0) list.Add(new { id = r.GetGuid(0), date = r.GetFieldValue<DateOnly>(1), reference = r.IsDBNull(2) ? null : r.GetString(2), total = r.GetDecimal(3), open = r.GetDecimal(4) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> SupplierStatementAsync(Guid uid, Guid supplierId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        var name = await Scalar<string>(s, "select name from procurement.supplier where id=@i", ct, ("i", supplierId));
        if (name is null) return ServiceResult<object>.Fail("Supplier not found.", 404);
        var rows = new List<(string Type, DateOnly Date, decimal Debit, decimal Credit, Guid Id)>();
        await using (var c = s.Command(@"select 'Purchase' t, receipt_date d, created_at, total, 0::numeric, id from procurement.purchase_receipt where supplier_id=@s
            union all select 'Payment', payment_date, created_at, 0, amount, id from procurement.supplier_payment where supplier_id=@s
            union all select 'Return', return_date, created_at, 0, total, id from procurement.purchase_return where supplier_id=@s
            order by 2, 3"))
        {
            c.Parameters.AddWithValue("s", supplierId);
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) rows.Add((r.GetString(0), r.GetFieldValue<DateOnly>(1), r.GetDecimal(3), r.GetDecimal(4), r.GetGuid(5)));
        }
        decimal bal = 0; var entries = new List<object>();
        foreach (var x in rows) { bal += x.Debit - x.Credit; entries.Add(new { type = x.Type, date = x.Date, debit = x.Debit, credit = x.Credit, balance = bal, id = x.Id }); }
        return ServiceResult<object>.Ok(new { supplier = name, outstanding = bal, entries });
    }

    public async Task<ServiceResult<object>> SuppliersAsync(Guid uid, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        await using var c = s.Command(@"select s.id, s.name, s.phone, s.notes, s.is_active,
            coalesce((select sum(total) from procurement.purchase_receipt r where r.supplier_id=s.id),0)
          - coalesce((select sum(amount) from procurement.supplier_payment p where p.supplier_id=s.id),0)
                    - coalesce((select sum(total) from procurement.purchase_return x where x.supplier_id=s.id),0) as owed
            from procurement.supplier s order by s.is_active desc, lower(s.name)");
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new { id = r.GetGuid(0), name = r.GetString(1), phone = r.IsDBNull(2) ? null : r.GetString(2), notes = r.IsDBNull(3) ? null : r.GetString(3), isActive = r.GetBoolean(4), owed = r.GetDecimal(5) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> CreateSupplierAsync(Guid uid, SupplierCommand m, CancellationToken ct)
    {
        if (!PurchaseRules.TryValidateSupplier(m.Name, m.Phone, m.Notes, out var err)) return ServiceResult<object>.Fail(err!);
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        try
        {
            await using var s = await db.BeginAsync(uid, tid, ct);
            var id = Guid.NewGuid();
            await Exec(s, ct, "insert into procurement.supplier(tenant_id,id,name,phone,notes) values (@t,@i,@n,@p,@o)",
                ("t", tid), ("i", id), ("n", m.Name!.Trim()), ("p", Blank(m.Phone)), ("o", Blank(m.Notes)));
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id });
        }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("A supplier with this name already exists.", 409); }
    }

    public async Task<ServiceResult<object>> UpdateSupplierAsync(Guid uid, Guid id, SupplierCommand m, CancellationToken ct)
    {
        if (!PurchaseRules.TryValidateSupplier(m.Name, m.Phone, m.Notes, out var err)) return ServiceResult<object>.Fail(err!);
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        try
        {
            await using var s = await db.BeginAsync(uid, tid, ct);
            await using var c = s.Command("update procurement.supplier set name=@n, phone=@p, notes=@o, is_active=@a where id=@i");
            c.Parameters.AddWithValue("n", m.Name!.Trim()); c.Parameters.AddWithValue("p", (object?)Blank(m.Phone) ?? DBNull.Value);
            c.Parameters.AddWithValue("o", (object?)Blank(m.Notes) ?? DBNull.Value); c.Parameters.AddWithValue("a", m.IsActive); c.Parameters.AddWithValue("i", id);
            if (await c.ExecuteNonQueryAsync(ct) == 0) return ServiceResult<object>.Fail("Supplier not found.", 404);
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id });
        }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("A supplier with this name already exists.", 409); }
    }

    public async Task<ServiceResult<object>> ReceiptsAsync(Guid uid, Guid? supplierId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        await using var c = s.Command(@"select r.id, r.receipt_date, s.name, r.supplier_ref, r.total, exists(select 1 from procurement.invoice_file f where f.tenant_id=r.tenant_id and f.receipt_id=r.id)
            from procurement.purchase_receipt r join procurement.supplier s on s.id=r.supplier_id
            where (@s::uuid is null or r.supplier_id=@s) order by r.created_at desc limit 100");
        c.Parameters.AddWithValue("s", (object?)supplierId ?? DBNull.Value);
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new { id = r.GetGuid(0), date = r.GetFieldValue<DateOnly>(1), supplier = r.GetString(2), supplierRef = r.IsDBNull(3) ? null : r.GetString(3), total = r.GetDecimal(4), hasInvoice = r.GetBoolean(5) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> PaymentsAsync(Guid uid, Guid? supplierId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        await using var c = s.Command(@"select p.id, p.payment_date, s.name, p.amount, p.method, p.reference
            from procurement.supplier_payment p join procurement.supplier s on s.id=p.supplier_id
            where (@s::uuid is null or p.supplier_id=@s) order by p.payment_date desc, p.created_at desc limit 100");
        c.Parameters.AddWithValue("s", (object?)supplierId ?? DBNull.Value);
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new { id = r.GetGuid(0), date = r.GetFieldValue<DateOnly>(1), supplier = r.GetString(2), amount = r.GetDecimal(3), method = r.GetString(4), reference = r.IsDBNull(5) ? null : r.GetString(5) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> ReceiveAsync(Guid uid, string? key, ReceiptCommand m, string correlationId, CancellationToken ct)
    {
        key ??= "";
        if (key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        if (!PurchaseRules.TryValidateReceipt(m.Lines, m.ReceiptDate, DateOnly.FromDateTime(DateTime.UtcNow), out var lines, out var err))
            return ServiceResult<object>.Fail(err!);
        if ((m.SupplierRef?.Trim().Length ?? 0) > 60) return ServiceResult<object>.Fail("Supplier reference is too long.");
        var hash = Hash(string.Join('|', m.SupplierId, m.LocationId, m.ReceiptDate.ToString("O"), m.SupplierRef?.Trim(),
            string.Join(';', lines.OrderBy(l => l.ProductId).Select(l => string.Join(':', l.ProductId, l.Quantity.Value.ToString(CultureInfo.InvariantCulture), l.UnitCost.Value.ToString(CultureInfo.InvariantCulture), l.Gst.Amount.ToString(CultureInfo.InvariantCulture))))));
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        var tenantId = tid.Value;

        try
        {
            await using var s = await db.BeginAsync(uid, tenantId, ct);
            var prior = await Prior(s, "procurement.purchase_receipt", key, hash, ct);
            if (prior is not null) return prior;

            if (await Scalar<Guid?>(s, "select id from procurement.supplier where id=@i and is_active for update", ct, ("i", m.SupplierId)) is null) return ServiceResult<object>.Fail("Unknown or inactive supplier.");
            if (await Scalar<Guid?>(s, "select id from core.stock_location where id=@i", ct, ("i", m.LocationId)) is null) return ServiceResult<object>.Fail("Unknown location.");
            var ids = lines.Select(l => l.ProductId).ToArray();
            await using (var q = s.Command("select count(*) from catalog.product where id = any(@i) and is_active"))
            {
                q.Parameters.AddWithValue("i", ids);
                if ((long)(await q.ExecuteScalarAsync(ct))! != ids.Length) return ServiceResult<object>.Fail("Unknown or inactive product.");
            }

            var gstClaimed = PurchaseRules.CanClaimGst(await Scalar<string>(s, "select gst_status from core.business_profile limit 1", ct));
            var total = PurchaseRules.GrandTotal(lines);
            var gstTotal = PurchaseRules.GstTotal(lines);
            var journal = PurchaseRules.ReceiptJournal(lines, gstClaimed);
            var accounts = new List<Guid>();
            foreach (var j in journal) accounts.Add(await AccountAsync(s, tenantId, j.AccountCode, ct));

            var id = Guid.NewGuid(); var entry = Guid.NewGuid();
            await Exec(s, ct, "insert into audit.audit_event(tenant_id,id,actor_user_id,action,entity_type,entity_id,summary,correlation_id) values (@t,@a,@u,'PurchaseReceived','PurchaseReceipt',@d,@s,@c)",
                ("t", tenantId), ("a", Guid.NewGuid()), ("u", uid), ("d", id), ("s", string.Create(CultureInfo.InvariantCulture, $"{lines.Count} lines, total {total.Amount:0.00} INR")), ("c", correlationId));
            await Exec(s, ct, "insert into accounting.journal_entry(tenant_id,id,effective_date,source_type,source_id) values (@t,@e,@d,'PurchaseReceipt',@s)", ("t", tenantId), ("e", entry), ("d", m.ReceiptDate), ("s", id));
            for (var i = 0; i < journal.Count; i++)
                await Exec(s, ct, "insert into accounting.journal_line(tenant_id,id,entry_id,account_id,debit,credit) values (@t,@i,@e,@a,@d,@c)",
                    ("t", tenantId), ("i", Guid.NewGuid()), ("e", entry), ("a", accounts[i]), ("d", journal[i].Debit), ("c", journal[i].Credit));
            await Exec(s, ct, "insert into procurement.purchase_receipt(tenant_id,id,supplier_id,location_id,receipt_date,supplier_ref,total,gst_total,gst_claimed,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@s,@l,@d,@r,@v,@g,@gc,@k,@h,@j,@u)",
                ("t", tenantId), ("i", id), ("s", m.SupplierId), ("l", m.LocationId), ("d", m.ReceiptDate), ("r", Blank(m.SupplierRef)), ("v", total.Amount), ("g", gstTotal.Amount), ("gc", gstClaimed), ("k", key), ("h", hash), ("j", entry), ("u", uid));

            // Lock affected sales first, in a stable global order, before locking
            // their commitments. Returns and cancellations use the same lock order.
            var affectedSales = new List<Guid>();
            await using (var q = s.Command(@"select distinct sale_id
                from inventory.negative_stock_commitment
                where tenant_id=@t and location_id=@l and product_id=any(@p)
                  and status='Open' and remaining_quantity>0
                order by sale_id"))
            {
                q.Parameters.AddWithValue("t", tenantId);
                q.Parameters.AddWithValue("l", m.LocationId);
                q.Parameters.AddWithValue("p", lines.Select(x => x.ProductId).Distinct().ToArray());
                await using var r = await q.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) affectedSales.Add(r.GetGuid(0));
            }
            foreach (var affectedSaleId in affectedSales)
                _ = await Scalar<Guid?>(s, "select id from sales.sale where id=@s for update", ct, ("s", affectedSaleId));

            var settledProvisionalTotal = 0m;
            var settledActualTotal = 0m;
            foreach (var l in lines)
            {
                var landed = l.LandedUnitCost(gstClaimed);
                var landedCost = UnitCost.TryCreate(landed, out var lc, out _) ? lc : l.UnitCost;
                await Exec(s, ct, "insert into procurement.purchase_receipt_line(tenant_id,receipt_id,product_id,quantity,unit_cost,gst_amount,landed_unit_cost) values (@t,@r,@p,@q,@c,@g,@lc)",
                    ("t", tenantId), ("r", id), ("p", l.ProductId), ("q", l.Quantity.Value),
                    ("c", l.UnitCost.Value), ("g", l.Gst.Amount), ("lc", landedCost.Value));

                var commitments = new List<(Guid Id, Guid SaleId, Guid SaleLineId, decimal RemainingQuantity, decimal ProvisionalUnitCost, decimal RemainingCost)>();
                await using (var q = s.Command(@"select id, sale_id, sale_line_id, remaining_quantity,
                        provisional_unit_cost, remaining_provisional_cost_amount
                    from inventory.negative_stock_commitment
                    where tenant_id=@t and product_id=@p and location_id=@l
                      and status='Open' and remaining_quantity>0
                    order by created_at, id
                    for update"))
                {
                    q.Parameters.AddWithValue("t", tenantId);
                    q.Parameters.AddWithValue("p", l.ProductId);
                    q.Parameters.AddWithValue("l", m.LocationId);
                    await using var r = await q.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                        commitments.Add((r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5)));
                }

                var receiptRemaining = l.Quantity.Value;
                var lineProvisionalSettled = 0m;
                var lineActualSettled = 0m;
                foreach (var commitment in commitments)
                {
                    if (receiptRemaining <= 0) break;
                    var allocatedQuantity = Math.Min(receiptRemaining, commitment.RemainingQuantity);
                    var provisionalCost = allocatedQuantity >= commitment.RemainingQuantity
                        ? commitment.RemainingCost
                        : Money.Of(allocatedQuantity * commitment.ProvisionalUnitCost).Amount;
                    var actualCost = Money.Of(allocatedQuantity * landedCost.Value).Amount;
                    var newRemainingQuantity = commitment.RemainingQuantity - allocatedQuantity;
                    var newRemainingCost = Math.Max(0m, commitment.RemainingCost - provisionalCost);
                    await Exec(s, ct, @"update inventory.negative_stock_commitment
                        set remaining_quantity=@rq, remaining_provisional_cost_amount=@rc,
                            settled_quantity=settled_quantity+@sq, settled_cost_amount=settled_cost_amount+@sc,
                            status=case when @rq=0 then 'Settled' else 'Open' end, updated_at=now()
                        where tenant_id=@t and id=@i",
                        ("rq", newRemainingQuantity), ("rc", newRemainingCost), ("sq", allocatedQuantity),
                        ("sc", actualCost), ("t", tenantId), ("i", commitment.Id));
                    await Exec(s, ct, @"update sales.sale_line
                        set inventory_cost_amount=inventory_cost_amount+@actual,
                            provisional_cost_amount=greatest(0,provisional_cost_amount-@provisional),
                            unit_cost=round((inventory_cost_amount+@actual+greatest(0,provisional_cost_amount-@provisional))/quantity,4)
                        where tenant_id=@t and sale_id=@s and line_id=@line",
                        ("actual", actualCost), ("provisional", provisionalCost), ("t", tenantId),
                        ("s", commitment.SaleId), ("line", commitment.SaleLineId));
                    await Exec(s, ct, @"update sales.sale
                        set inventory_cost_total=inventory_cost_total+@actual,
                            provisional_cost_total=greatest(0,provisional_cost_total-@provisional),
                            cost_total=greatest(0,cost_total+@actual-@provisional)
                        where tenant_id=@t and id=@s",
                        ("actual", actualCost), ("provisional", provisionalCost), ("t", tenantId), ("s", commitment.SaleId));
                    receiptRemaining -= allocatedQuantity;
                    lineProvisionalSettled += provisionalCost;
                    lineActualSettled += actualCost;
                }

                var stockValue = l.StockValue(gstClaimed).Amount;
                var remainingStockValue = Math.Max(0m, stockValue - lineActualSettled);
                await Exec(s, ct, "insert into inventory.stock_movement(tenant_id,id,product_id,location_id,quantity_delta,unit_cost,value_delta,effective_date,source_type,source_id) values (@t,@i,@p,@l,@q,@c,@v,@e,'PurchaseReceipt',@d)",
                    ("t", tenantId), ("i", Guid.NewGuid()), ("p", l.ProductId), ("l", m.LocationId),
                    ("q", l.Quantity.Value), ("c", landedCost.Value), ("v", remainingStockValue), ("e", m.ReceiptDate), ("d", id));
                await Exec(s, ct, "insert into inventory.stock_balance(tenant_id,product_id,location_id,quantity) values (@t,@p,@l,@q) on conflict (tenant_id,product_id,location_id) do update set quantity = inventory.stock_balance.quantity + excluded.quantity",
                    ("t", tenantId), ("p", l.ProductId), ("l", m.LocationId), ("q", l.Quantity.Value));

                if (receiptRemaining > 0 && Quantity.TryCreate(receiptRemaining, out var remainingQty, out _))
                {
                    var current = await ReadCostAsync(s, l.ProductId, ct);
                    var next = WeightedAverageCostCalculator.ApplyReceipt(current, remainingQty, landedCost);
                    await Exec(s, ct, "insert into inventory.item_cost(tenant_id,product_id,total_quantity,inventory_value,wac) values (@t,@p,@q,@v,@w) on conflict (tenant_id,product_id) do update set total_quantity=excluded.total_quantity, inventory_value=excluded.inventory_value, wac=excluded.wac",
                        ("t", tenantId), ("p", l.ProductId), ("q", next.TotalQuantity), ("v", next.InventoryValue), ("w", next.Wac));
                }
                settledProvisionalTotal += lineProvisionalSettled;
                settledActualTotal += lineActualSettled;
            }

            if (settledProvisionalTotal > 0)
            {
                var adjustment = new List<JournalLineDraft>
                {
                    new(SalesRules.NegativeStockClearingAccount, settledProvisionalTotal, 0m)
                };
                if (settledActualTotal > 0)
                    adjustment.Add(new(SalesRules.InventoryAccount, 0m, settledActualTotal));
                var variance = settledActualTotal - settledProvisionalTotal;
                if (variance > 0) adjustment.Add(new(SalesRules.CogsAccount, variance, 0m));
                else if (variance < 0) adjustment.Add(new(SalesRules.CogsAccount, 0m, -variance));
                BalancedJournalPolicy.AssertBalanced(adjustment);
                var adjustmentEntry = Guid.NewGuid();
                await Exec(s, ct, "insert into accounting.journal_entry(tenant_id,id,effective_date,source_type,source_id) values (@t,@e,@d,'NegativeStockReceiptAdjustment',@s)",
                    ("t", tenantId), ("e", adjustmentEntry), ("d", m.ReceiptDate), ("s", id));
                foreach (var line in adjustment)
                {
                    var account = await AccountAsync(s, tenantId, line.AccountCode, ct);
                    await Exec(s, ct, "insert into accounting.journal_line(tenant_id,id,entry_id,account_id,debit,credit) values (@t,@i,@e,@a,@d,@c)",
                        ("t", tenantId), ("i", Guid.NewGuid()), ("e", adjustmentEntry), ("a", account),
                        ("d", line.Debit), ("c", line.Credit));
                }
                await Exec(s, ct, "insert into audit.audit_event(tenant_id,id,actor_user_id,action,entity_type,entity_id,summary,correlation_id) values (@t,@a,@u,'NegativeStockSettled','PurchaseReceipt',@d,@s,@c)",
                    ("t", tenantId), ("a", Guid.NewGuid()), ("u", uid), ("d", id),
                    ("s", string.Create(CultureInfo.InvariantCulture,
                        $"Settled negative stock provisional cost {settledProvisionalTotal:0.00} INR against actual receipt cost {settledActualTotal:0.00} INR")),
                    ("c", correlationId));
            }

            if (m.InvoiceFileId is { } fid)
            {
                await using var att = s.Command("update procurement.invoice_file set receipt_id=@r where id=@f and receipt_id is null");
                att.Parameters.AddWithValue("r", id); att.Parameters.AddWithValue("f", fid);
                if (await att.ExecuteNonQueryAsync(ct) != 1) return ServiceResult<object>.Fail("Invoice file is missing or already used.");
            }
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id, total = total.Amount });
        }
        catch (PostgresException e) when (e.SqlState == "23505")
        {
            await using var s2 = await db.BeginAsync(uid, tenantId, ct);
            return await Prior(s2, "procurement.purchase_receipt", key, hash, ct) ?? ServiceResult<object>.Fail("Duplicate receipt.", 409);
        }
    }

    public async Task<ServiceResult<object>> PayAsync(Guid uid, string? key, PaymentCommand m, string correlationId, CancellationToken ct)
    {
        key ??= "";
        if (key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        if ((m.Reference?.Trim().Length ?? 0) > 60) return ServiceResult<object>.Fail("Reference is too long.");
        var hash = Hash(string.Join('|', m.SupplierId, m.PaymentDate.ToString("O"), m.Amount.ToString(CultureInfo.InvariantCulture), m.Method, m.Reference?.Trim(), string.Join(',', (m.Allocations ?? []).Select(a => a.BillId.ToString() + ':' + a.Amount.ToString(CultureInfo.InvariantCulture)))));
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        var tenantId = tid.Value;

        try
        {
            await using var s = await db.BeginAsync(uid, tenantId, ct);
            var prior = await Prior(s, "procurement.supplier_payment", key, hash, ct);
            if (prior is not null) return prior;

            if (await Scalar<Guid?>(s, "select id from procurement.supplier where id=@i for update", ct, ("i", m.SupplierId)) is null) return ServiceResult<object>.Fail("Unknown supplier.");
            var owed = await Scalar<decimal?>(s, @"select coalesce((select sum(total) from procurement.purchase_receipt where supplier_id=@i),0)
                - coalesce((select sum(amount) from procurement.supplier_payment where supplier_id=@i),0)
                                - coalesce((select sum(total) from procurement.purchase_return where supplier_id=@i),0)", ct, ("i", m.SupplierId)) ?? 0m;
            if (!PurchaseRules.TryValidatePayment(m.Amount, m.Method, m.PaymentDate, DateOnly.FromDateTime(DateTime.UtcNow), owed, out var money, out var err))
                return ServiceResult<object>.Fail(err!);

            var journal = PurchaseRules.PaymentJournal(money);
            var accounts = new List<Guid>();
            foreach (var j in journal) accounts.Add(await AccountAsync(s, tenantId, j.AccountCode, ct));

            var id = Guid.NewGuid(); var entry = Guid.NewGuid();
            await Exec(s, ct, "insert into audit.audit_event(tenant_id,id,actor_user_id,action,entity_type,entity_id,summary,correlation_id) values (@t,@a,@u,'SupplierPaid','SupplierPayment',@d,@s,@c)",
                ("t", tenantId), ("a", Guid.NewGuid()), ("u", uid), ("d", id), ("s", string.Create(CultureInfo.InvariantCulture, $"{money.Amount:0.00} INR via {m.Method}")), ("c", correlationId));
            await Exec(s, ct, "insert into accounting.journal_entry(tenant_id,id,effective_date,source_type,source_id) values (@t,@e,@d,'SupplierPayment',@s)", ("t", tenantId), ("e", entry), ("d", m.PaymentDate), ("s", id));
            for (var i = 0; i < journal.Count; i++)
                await Exec(s, ct, "insert into accounting.journal_line(tenant_id,id,entry_id,account_id,debit,credit) values (@t,@i,@e,@a,@d,@c)",
                    ("t", tenantId), ("i", Guid.NewGuid()), ("e", entry), ("a", accounts[i]), ("d", journal[i].Debit), ("c", journal[i].Credit));
            await Exec(s, ct, "insert into procurement.supplier_payment(tenant_id,id,supplier_id,payment_date,amount,method,reference,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@s,@d,@a,@m,@r,@k,@h,@j,@u)",
                ("t", tenantId), ("i", id), ("s", m.SupplierId), ("d", m.PaymentDate), ("a", money.Amount), ("m", m.Method), ("r", Blank(m.Reference)), ("k", key), ("h", hash), ("j", entry), ("u", uid));
            var allocErr = await AllocateAsync(s, tenantId, "procurement.supplier_payment_allocation", "receipt_id", OpenReceiptSql, m.SupplierId, id, money.Amount, m.Allocations, ct);
            if (allocErr is not null) return ServiceResult<object>.Fail(allocErr);
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id, owed = owed - money.Amount });
        }
        catch (PostgresException e) when (e.SqlState == "23505")
        {
            await using var s2 = await db.BeginAsync(uid, tenantId, ct);
            return await Prior(s2, "procurement.supplier_payment", key, hash, ct) ?? ServiceResult<object>.Fail("Duplicate payment.", 409);
        }
    }

    static async Task<Guid> AccountAsync(TenantSession s, Guid tenantId, string code, CancellationToken ct)
    {
        var (name, type) = code switch { "2000" => ("Accounts Payable", "Liability"), "1000" => ("Cash and Bank", "Asset"), "1400" => ("Inventory", "Asset"), "1410" => ("Negative Stock Clearing", "Liability"), "5000" => ("Cost of Goods Sold", "Expense"), "1500" => ("GST Input", "Asset"), _ => (code, "Asset") };
        await Exec(s, ct, "insert into accounting.account(tenant_id,id,code,name,type) values (@t,@i,@c,@n,@y) on conflict (tenant_id,code) do nothing",
            ("t", tenantId), ("i", Guid.NewGuid()), ("c", code), ("n", name), ("y", type));
        return (await Scalar<Guid?>(s, "select id from accounting.account where code=@c", ct, ("c", code)))!.Value;
    }

    static async Task<CostState> ReadCostAsync(TenantSession s, Guid productId, CancellationToken ct)
    {
        await using var q = s.Command("select total_quantity,inventory_value,wac from inventory.item_cost where product_id=@p for update");
        q.Parameters.AddWithValue("p", productId);
        await using var r = await q.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new CostState(r.GetDecimal(0), r.GetDecimal(1), r.GetDecimal(2)) : CostState.Empty;
    }
}
