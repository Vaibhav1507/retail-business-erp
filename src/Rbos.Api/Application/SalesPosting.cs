using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Rbos.Api.Persistence;
using static Rbos.Api.Application.SqlHelpers;
using Rbos.Domain;

namespace Rbos.Api.Application;

public sealed record CustomerCommand(string? Name, string? Phone, string? Notes, bool IsActive = true, string? WhatsappConsentStatus = null);
public sealed record SalePaymentInput(string Method, decimal Amount);
public sealed record SaleCommand(Guid? CustomerId, Guid LocationId, DateOnly SaleDate, string? SaleType, decimal? AmountPaid, string? PayMethod, List<SaleLineInput>? Lines, decimal? GstRate = null, List<SalePaymentInput>? Payments = null, string? PlaceOfSupply = null);

/// <summary>Customers and sales (stock deduction at WAC, balanced journal, customer receivable). All amounts are recomputed server-side.</summary>
public sealed class SalesService(TenantDb db)
{
    public async Task<ServiceResult<object>> CustomersAsync(Guid uid, string? q, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        await using var c = s.Command(@"select c.id, c.name, c.phone, c.notes, c.is_active, c.whatsapp_invoice_consent_status,
            coalesce((select sum(total - amount_paid) from sales.sale x where x.customer_id=c.id and not exists (select 1 from sales.sale_cancellation k where k.sale_id=x.id)),0)
                      - coalesce((select sum(amount) from sales.customer_payment p where p.customer_id=c.id),0)
                      - coalesce((select sum(receivable_credit) from sales.sale_return r where r.customer_id=c.id),0) as owed
            from sales.customer c
            where (@q::text is null or lower(c.name) like '%' || lower(@q) || '%' or coalesce(c.phone,'') like '%' || @q || '%')
            order by c.is_active desc, lower(c.name) limit 200");
        c.Parameters.AddWithValue("q", (object?)Blank(q) ?? DBNull.Value);
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new { id = r.GetGuid(0), name = r.GetString(1), phone = r.IsDBNull(2) ? null : r.GetString(2), notes = r.IsDBNull(3) ? null : r.GetString(3), isActive = r.GetBoolean(4), whatsappConsentStatus = r.GetString(5), owed = r.GetDecimal(6) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> CreateCustomerAsync(Guid uid, CustomerCommand m, CancellationToken ct)
    {
        if (m.WhatsappConsentStatus is not null and not ("Unknown" or "OptedIn" or "OptedOut")) return ServiceResult<object>.Fail("Invalid WhatsApp consent status.");
        if (!SalesRules.TryValidateCustomer(m.Name, m.Phone, m.Notes, out var err)) return ServiceResult<object>.Fail(err!);
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        var id = Guid.NewGuid();
        await Exec(s, ct, "insert into sales.customer(tenant_id,id,name,phone,notes,whatsapp_invoice_consent_status,whatsapp_invoice_consent_updated_at) values (@t,@i,@n,@p,@o,@cs,case when @cs='Unknown' then null else now() end)",
            ("t", tid), ("i", id), ("n", m.Name!.Trim()), ("p", Blank(m.Phone)), ("o", Blank(m.Notes)), ("cs", m.WhatsappConsentStatus ?? "Unknown"));
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id });
    }

    public async Task<ServiceResult<object>> UpdateCustomerAsync(Guid uid, Guid id, CustomerCommand m, CancellationToken ct)
    {
        if (m.WhatsappConsentStatus is not null and not ("Unknown" or "OptedIn" or "OptedOut")) return ServiceResult<object>.Fail("Invalid WhatsApp consent status.");
        if (!SalesRules.TryValidateCustomer(m.Name, m.Phone, m.Notes, out var err)) return ServiceResult<object>.Fail(err!);
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        await using var c = s.Command("update sales.customer set name=@n, phone=@p, notes=@o, is_active=@a, whatsapp_invoice_consent_status=coalesce(@cs, whatsapp_invoice_consent_status), whatsapp_invoice_consent_updated_at=case when @cs is null then whatsapp_invoice_consent_updated_at when @cs='Unknown' then null else now() end where id=@i");
        c.Parameters.AddWithValue("n", m.Name!.Trim()); c.Parameters.AddWithValue("p", (object?)Blank(m.Phone) ?? DBNull.Value);
        c.Parameters.AddWithValue("o", (object?)Blank(m.Notes) ?? DBNull.Value); c.Parameters.AddWithValue("a", m.IsActive); c.Parameters.AddWithValue("cs", (object?)m.WhatsappConsentStatus ?? DBNull.Value); c.Parameters.AddWithValue("i", id);
        if (await c.ExecuteNonQueryAsync(ct) == 0) return ServiceResult<object>.Fail("Customer not found.", 404);
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id });
    }

    public async Task<ServiceResult<object>> SalesAsync(Guid uid, string? q, Guid? customerId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        await using var c = s.Command(@"select x.id, x.sale_date, x.sale_type, x.total, x.amount_paid, c.name, x.number,
            exists (select 1 from sales.sale_cancellation k where k.sale_id=x.id), x.invoice_no,
            (select d.status from sales.invoice_delivery d where d.tenant_id=x.tenant_id and d.sale_id=x.id and d.channel='WhatsApp')
            from sales.sale x left join sales.customer c on c.id=x.customer_id
            where (@c::uuid is null or x.customer_id=@c)
              and (@q::text is null or lower(coalesce(c.name,'walk-in')) like '%' || lower(@q) || '%'
              or x.number::text = @q or lower(x.sale_type)=lower(@q) or coalesce(c.phone,'') like '%' || @q || '%'
              or exists (select 1 from sales.sale_line sl where sl.sale_id=x.id and upper(btrim(@q)) = any(sl.serials)))
            order by x.sale_date desc, x.created_at desc limit 100");
        c.Parameters.AddWithValue("c", (object?)customerId ?? DBNull.Value);
        c.Parameters.AddWithValue("q", (object?)Blank(q) ?? DBNull.Value);
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new { id = r.GetGuid(0), date = r.GetFieldValue<DateOnly>(1), saleType = r.GetString(2), total = r.GetDecimal(3), amountPaid = r.GetDecimal(4), balance = r.GetDecimal(3) - r.GetDecimal(4), customer = r.IsDBNull(5) ? "Walk-in" : r.GetString(5),
                number = r.GetInt64(6), cancelled = r.GetBoolean(7), invoiceNo = r.IsDBNull(8) ? null : r.GetString(8), whatsappStatus = r.IsDBNull(9) ? "NotAvailable" : r.GetString(9) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> SaleAsync(Guid uid, Guid id, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        object? head = null;
        await using (var c = s.Command(@"select x.id, x.sale_date, x.sale_type, x.total, x.amount_paid, x.pay_method, c.name, l.name, x.number, c.phone,
            c.notes, x.gst_rate, exists (select 1 from sales.sale_cancellation k where k.sale_id=x.id), x.invoice_no,
            (select d.status from sales.invoice_delivery d where d.tenant_id=x.tenant_id and d.sale_id=x.id and d.channel='WhatsApp')
            from sales.sale x left join sales.customer c on c.id=x.customer_id join core.stock_location l on l.id=x.location_id where x.id=@i"))
        {
            c.Parameters.AddWithValue("i", id);
            await using var r = await c.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
                head = new { id = r.GetGuid(0), date = r.GetFieldValue<DateOnly>(1), saleType = r.GetString(2), total = r.GetDecimal(3), amountPaid = r.GetDecimal(4), balance = r.GetDecimal(3) - r.GetDecimal(4), payMethod = r.IsDBNull(5) ? null : r.GetString(5), customer = r.IsDBNull(6) ? "Walk-in" : r.GetString(6), location = r.GetString(7), number = r.GetInt64(8), customerPhone = r.IsDBNull(9) ? null : r.GetString(9),
                cancelled = r.GetBoolean(12), invoiceNo = r.IsDBNull(13) ? null : r.GetString(13), whatsappStatus = r.IsDBNull(14) ? "NotAvailable" : r.GetString(14) };
                        }
        if (head is null) return ServiceResult<object>.Fail("Sale not found.", 404);
        var lines = new List<object>();
        await using (var c = s.Command(@"select p.name, p.internal_sku, l.quantity, l.unit_price, l.discount, l.line_total, l.serials
            from sales.sale_line l join catalog.product p on p.id=l.product_id where l.sale_id=@i order by p.name"))
        {
            c.Parameters.AddWithValue("i", id);
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                lines.Add(new { product = r.GetString(0), sku = r.GetString(1), quantity = r.GetDecimal(2), unitPrice = r.GetDecimal(3), discount = r.GetDecimal(4), lineTotal = r.GetDecimal(5), serials = r.IsDBNull(6) ? Array.Empty<string>() : r.GetFieldValue<string[]>(6) });
        }
        return ServiceResult<object>.Ok(new { sale = head, lines });
    }

    public async Task<ServiceResult<object>> RetryWhatsAppInvoiceAsync(Guid uid, Guid saleId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid, ct);
        Guid deliveryId;
        string? recipient;
        string consent;
        string status;
        await using (var q = s.Command(@"select d.id, c.phone, coalesce(c.whatsapp_invoice_consent_status,'Unknown'), d.status
            from sales.invoice_delivery d
            join sales.sale x on x.tenant_id=d.tenant_id and x.id=d.sale_id
            left join sales.customer c on c.tenant_id=x.tenant_id and c.id=x.customer_id
            where d.tenant_id=@t and d.sale_id=@s and d.channel='WhatsApp' for update of d"))
        {
            q.Parameters.AddWithValue("t", tid.Value);
            q.Parameters.AddWithValue("s", saleId);
            await using var r = await q.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return ServiceResult<object>.Fail("WhatsApp invoice record not found.", 404);
            deliveryId = r.GetGuid(0);
            recipient = r.IsDBNull(1) ? null : r.GetString(1);
            consent = r.GetString(2);
            status = r.GetString(3);
        }
        if (status is not ("Failed" or "WaitingForSetup" or "Retry" or "ConsentRequired" or "Suppressed" or "NoRecipient" or "NeedsReview"))
            return ServiceResult<object>.Fail("Only a failed or pending-setup invoice can be retried.", 409);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            await Exec(s, ct, "update sales.invoice_delivery set status='NoRecipient', last_error=null, updated_at=now() where tenant_id=@t and id=@i",
                ("t", tid.Value), ("i", deliveryId));
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { status = "NoRecipient" });
        }
        if (consent != "OptedIn")
        {
            var blocked = consent == "OptedOut" ? "Suppressed" : "ConsentRequired";
            await Exec(s, ct, "update sales.invoice_delivery set status=@st, last_error=null, updated_at=now() where tenant_id=@t and id=@i",
                ("st", blocked), ("t", tid.Value), ("i", deliveryId));
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { status = blocked });
        }
        await Exec(s, ct, "update sales.invoice_delivery set status='Pending', recipient=@r, last_error=null, updated_at=now() where tenant_id=@t and id=@i",
            ("r", recipient), ("t", tid.Value), ("i", deliveryId));
        await using (var enqueue = s.Command("select integrations.enqueue_invoice_delivery(@t,@d,@s,@u)"))
        {
            enqueue.Parameters.AddWithValue("t", tid.Value);
            enqueue.Parameters.AddWithValue("d", deliveryId);
            enqueue.Parameters.AddWithValue("s", saleId);
            enqueue.Parameters.AddWithValue("u", uid);
            await enqueue.ExecuteNonQueryAsync(ct);
        }
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { status = "Pending" });
    }

    static bool TrySplitPayments(List<SalePaymentInput> input, out List<(string Method, decimal Amount)> split, out string? error)
    {
        split = [];
        foreach (var p in input)
        {
            if (p.Method is null || !SalesRules.PayMethods.Contains(p.Method)) { error = "Payment method must be Cash, Bank or UPI."; return false; }
            if (p.Amount <= 0 || decimal.Round(p.Amount, 2) != p.Amount) { error = "Each payment amount must be positive with at most 2 decimal places."; return false; }
            if (split.Any(x => x.Method == p.Method)) { error = "Each payment method can appear only once."; return false; }
            split.Add((p.Method, p.Amount));
        }
        error = null; return true;
    }

    public async Task<ServiceResult<object>> SellAsync(Guid uid, string? key, SaleCommand m, string correlationId, CancellationToken ct)
    {
        key ??= "";
        if (key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        var amountPaid = m.AmountPaid; var payMethod = m.PayMethod;
        var split = new List<(string Method, decimal Amount)>();
        if (m.Payments is { Count: > 0 })
        {
            if (!TrySplitPayments(m.Payments, out split, out var splitErr)) return ServiceResult<object>.Fail(splitErr!);
            amountPaid = split.Sum(x => x.Amount); payMethod = split.OrderByDescending(x => x.Amount).First().Method;
        }
        if (!SalesRules.TryValidateSale(m.Lines, m.SaleType, amountPaid, payMethod, m.CustomerId is not null, m.SaleDate, DateOnly.FromDateTime(DateTime.UtcNow),
                out var lines, out var total, out var paid, out var method, out var err))
            return ServiceResult<object>.Fail(err!);
        var splitKey = split.Count == 0 ? "" : "|" + string.Join(',', split.OrderBy(x => x.Method).Select(x => $"{x.Method}:{x.Amount.ToString(CultureInfo.InvariantCulture)}"));
        if (split.Count == 0 && paid.Amount > 0) split.Add((method!, paid.Amount));
        var hash = Hash(string.Join('|', m.CustomerId, m.LocationId, m.SaleDate.ToString("O"), m.SaleType, paid.Amount.ToString(CultureInfo.InvariantCulture), method,
            string.Join(';', lines.Select(l => $"{l.ProductId}:{l.Quantity.Value}:{l.UnitPrice}:{l.Discount}:{string.Join(',', l.Serials ?? [])}"))) + splitKey);
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        var tenantId = tid.Value;

        try
        {
            await using var s = await db.BeginAsync(uid, tenantId, ct);
            await using (var q = s.Command("select id, request_hash from sales.sale where idempotency_key=@k"))
            {
                q.Parameters.AddWithValue("k", key);
                await using var r = await q.ExecuteReaderAsync(ct);
                if (await r.ReadAsync(ct))
                    return r.GetString(1) == hash ? ServiceResult<object>.Ok(new { id = r.GetGuid(0), replayed = true }) : ServiceResult<object>.Fail("Idempotency-Key reused with a different request.", 409);
            }
            if (m.CustomerId is not null && await Scalar<Guid?>(s, "select id from sales.customer where id=@i and is_active", ct, ("i", m.CustomerId)) is null) return ServiceResult<object>.Fail("Unknown or inactive customer.");
            if (await Scalar<Guid?>(s, "select id from core.stock_location where id=@i", ct, ("i", m.LocationId)) is null) return ServiceResult<object>.Fail("Unknown location.");
            var ids = lines.Select(l => l.ProductId).ToArray();
            await using (var q = s.Command("select count(*) from catalog.product where id = any(@i) and is_active"))
            {
                q.Parameters.AddWithValue("i", ids);
                if ((long)(await q.ExecuteScalarAsync(ct))! != ids.Length) return ServiceResult<object>.Fail("Unknown or inactive product.");
            }

            var costed = new List<(SaleLine Line, decimal UnitCost, decimal Cost, CostState Next)>();
            foreach (var l in lines.OrderBy(x => x.ProductId))
            {
                var onHand = await Scalar<decimal?>(s, "select quantity from inventory.stock_balance where product_id=@p and location_id=@l for update", ct, ("p", l.ProductId), ("l", m.LocationId)) ?? 0m;
                if (onHand < l.Quantity.Value)
                {
                    var name = await Scalar<string>(s, "select name from catalog.product where id=@p", ct, ("p", l.ProductId));
                    return ServiceResult<object>.Fail($"Not enough stock for {name}: {onHand:0.###} available.");
                }
                var cur = await ReadCostAsync(s, l.ProductId, ct);
                var remaining = cur.TotalQuantity - l.Quantity.Value;
                var cost = remaining <= 0 ? cur.InventoryValue : Math.Min(cur.InventoryValue, Money.Of(l.Quantity.Value * cur.Wac).Amount);
                costed.Add((l, cur.Wac, cost, new CostState(remaining, cur.InventoryValue - cost, cur.Wac)));
            }

            var costTotal = Money.Of(costed.Sum(x => x.Cost));
            var journal = SalesRules.SaleJournal(total, paid, costTotal);
            var accounts = new List<Guid>();
            foreach (var j in journal) accounts.Add(await AccountAsync(s, tenantId, j.AccountCode, ct));

            var id = Guid.NewGuid(); var entry = Guid.NewGuid();
            await Exec(s, ct, "insert into audit.audit_event(tenant_id,id,actor_user_id,action,entity_type,entity_id,summary,correlation_id) values (@t,@a,@u,'SalePosted','Sale',@d,@s,@c)",
                ("t", tenantId), ("a", Guid.NewGuid()), ("u", uid), ("d", id), ("s", string.Create(CultureInfo.InvariantCulture, $"{m.SaleType} sale, {lines.Count} lines, total {total.Amount:0.00} INR")), ("c", correlationId));
            await Exec(s, ct, "insert into accounting.journal_entry(tenant_id,id,effective_date,source_type,source_id) values (@t,@e,@d,'Sale',@s)", ("t", tenantId), ("e", entry), ("d", m.SaleDate), ("s", id));
            for (var i = 0; i < journal.Count; i++)
                await Exec(s, ct, "insert into accounting.journal_line(tenant_id,id,entry_id,account_id,debit,credit) values (@t,@i,@e,@a,@d,@c)",
                    ("t", tenantId), ("i", Guid.NewGuid()), ("e", entry), ("a", accounts[i]), ("d", journal[i].Debit), ("c", journal[i].Credit));
            await Exec(s, ct, "insert into sales.sale(tenant_id,id,customer_id,location_id,sale_date,sale_type,total,amount_paid,pay_method,cost_total,idempotency_key,request_hash,journal_entry_id,created_by,gst_rate,invoice_no,place_of_supply,is_inter_state) values (@t,@i,@cu,@l,@d,@y,@v,@pd,@pm,@ct,@k,@h,@j,@u,@g,@iv,@pos,(@pos::text is not null and exists (select 1 from core.business_profile b where b.state is not null and lower(btrim(b.state)) <> lower(btrim(@pos::text)))))",
                ("t", tenantId), ("i", id), ("cu", m.CustomerId), ("l", m.LocationId), ("d", m.SaleDate), ("y", m.SaleType), ("v", total.Amount), ("pd", paid.Amount), ("pm", method), ("ct", costTotal.Amount), ("k", key), ("h", hash), ("j", entry), ("u", uid), ("g", (object?)m.GstRate ?? DBNull.Value), ("iv", await NextInvoiceNoAsync(s, tenantId, m.SaleDate, ct)), ("pos", string.IsNullOrWhiteSpace(m.PlaceOfSupply) ? DBNull.Value : m.PlaceOfSupply.Trim()));

            foreach (var (payMethodName, payAmount) in split)
                await Exec(s, ct, "insert into sales.sale_payment(tenant_id,sale_id,method,amount) values (@t,@s,@m,@a)", ("t", tenantId), ("s", id), ("m", payMethodName), ("a", payAmount));

            foreach (var (l, unitCost, cost, next) in costed)
            {
                await Exec(s, ct, "insert into sales.sale_line(tenant_id,sale_id,product_id,quantity,unit_price,discount,line_total,unit_cost,serials) values (@t,@s,@p,@q,@u,@d,@v,@c,@sr)",
                    ("t", tenantId), ("s", id), ("p", l.ProductId), ("q", l.Quantity.Value), ("u", l.UnitPrice), ("d", l.Discount), ("v", l.LineTotal), ("c", unitCost), ("sr", l.Serials));
                await Exec(s, ct, "insert into inventory.stock_movement(tenant_id,id,product_id,location_id,quantity_delta,unit_cost,value_delta,effective_date,source_type,source_id) values (@t,@i,@p,@l,@q,@c,@v,@e,'Sale',@d)",
                    ("t", tenantId), ("i", Guid.NewGuid()), ("p", l.ProductId), ("l", m.LocationId), ("q", -l.Quantity.Value), ("c", unitCost), ("v", -cost), ("e", m.SaleDate), ("d", id));
                await Exec(s, ct, "update inventory.stock_balance set quantity = quantity - @q where product_id=@p and location_id=@l", ("q", l.Quantity.Value), ("p", l.ProductId), ("l", m.LocationId));
                await Exec(s, ct, "update inventory.item_cost set total_quantity=@q, inventory_value=@v, wac=@w where product_id=@p", ("q", next.TotalQuantity), ("v", next.InventoryValue), ("w", next.Wac), ("p", l.ProductId));
            }
            // Freeze the invoice from committed business data and enqueue only with explicit customer consent.
            // Both writes share the sale transaction, so a failed sale cannot leave a deliverable invoice.
            var deliveryId = Guid.NewGuid();
            await using (var snapshot = s.Command(@"
                insert into sales.invoice_snapshot(tenant_id, sale_id, invoice_no, payload)
                select @t, x.id, x.invoice_no,
                  jsonb_build_object(
                    'sale', jsonb_build_object(
                      'id', x.id, 'invoiceNo', x.invoice_no, 'date', x.sale_date,
                      'saleType', x.sale_type, 'total', x.total, 'amountPaid', x.amount_paid,
                      'payMethod', x.pay_method, 'gstRate', x.gst_rate
                    ),
                    'business', jsonb_build_object(
                      'businessName', b.display_name, 'address', b.address, 'city', b.city,
                      'state', b.state, 'pinCode', b.pin_code, 'mobile', b.mobile, 'gstin', b.gstin
                    ),
                    'customer', jsonb_build_object(
                      'name', coalesce(c.name, 'Walk-in customer'), 'phone', c.phone, 'notes', c.notes
                    ),
                    'location', l.name,
                    'lines', coalesce((
                      select jsonb_agg(jsonb_build_object(
                        'name', p.name, 'sku', p.internal_sku, 'quantity', sl.quantity,
                        'unitPrice', sl.unit_price, 'discount', sl.discount,
                        'lineTotal', sl.line_total, 'serials', to_jsonb(sl.serials)
                      ) order by p.name)
                      from sales.sale_line sl
                      join catalog.product p on p.tenant_id=sl.tenant_id and p.id=sl.product_id
                      where sl.tenant_id=x.tenant_id and sl.sale_id=x.id
                    ), '[]'::jsonb),
                    'payments', coalesce((
                      select jsonb_agg(jsonb_build_object('method', sp.method, 'amount', sp.amount) order by sp.method)
                      from sales.sale_payment sp where sp.tenant_id=x.tenant_id and sp.sale_id=x.id
                    ), '[]'::jsonb)
                  )
                from sales.sale x
                join core.business_profile b on b.tenant_id=x.tenant_id
                join core.stock_location l on l.tenant_id=x.tenant_id and l.id=x.location_id
                left join sales.customer c on c.tenant_id=x.tenant_id and c.id=x.customer_id
                where x.tenant_id=@t and x.id=@s
                "));
            snapshot.Parameters.AddWithValue("t", tenantId);
            snapshot.Parameters.AddWithValue("s", id);
            if (await snapshot.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Could not create the saved invoice snapshot.");

            var consent = await Scalar<string>(s,
                "select coalesce(c.whatsapp_invoice_consent_status, 'Unknown') from sales.sale x left join sales.customer c on c.tenant_id=x.tenant_id and c.id=x.customer_id where x.tenant_id=@t and x.id=@s",
                ct, ("t", tenantId), ("s", id)) ?? "Unknown";
            var recipient = await Scalar<string?>(s,
                "select c.phone from sales.sale x left join sales.customer c on c.tenant_id=x.tenant_id and c.id=x.customer_id where x.tenant_id=@t and x.id=@s",
                ct, ("t", tenantId), ("s", id));
            var deliveryStatus = string.IsNullOrWhiteSpace(recipient) ? "NoRecipient"
                : consent == "OptedOut" ? "Suppressed"
                : consent != "OptedIn" ? "ConsentRequired"
                : "Pending";
            await Exec(s, ct, @"insert into sales.invoice_delivery
                (tenant_id,id,sale_id,channel,status,recipient)
                values (@t,@i,@s,'WhatsApp',@st,@r)",
                ("t", tenantId), ("i", deliveryId), ("s", id), ("st", deliveryStatus), ("r", (object?)recipient ?? DBNull.Value));
            if (deliveryStatus == "Pending")
            {
                await using var enqueue = s.Command("select integrations.enqueue_invoice_delivery(@t,@d,@s,@u)");
                enqueue.Parameters.AddWithValue("t", tenantId);
                enqueue.Parameters.AddWithValue("d", deliveryId);
                enqueue.Parameters.AddWithValue("s", id);
                enqueue.Parameters.AddWithValue("u", uid);
                await enqueue.ExecuteNonQueryAsync(ct);
            }

            var allSerials = lines.Where(l => l.Serials is not null).SelectMany(l => l.Serials!).ToArray();
            var soldBefore = new List<string>();
            if (allSerials.Length > 0)
            {
                await using var q = s.Command("select distinct u from sales.sale_line l, unnest(l.serials) u where u = any(@s)");
                q.Parameters.AddWithValue("s", allSerials);
                await using var r = await q.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) soldBefore.Add(r.GetString(0));
            }
            await s.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id, total = total.Amount, balance = total.Amount - paid.Amount, serialWarnings = soldBefore });
        }
        catch (PostgresException e) when (e.SqlState == "23505")
        {
            await using var s2 = await db.BeginAsync(uid, tenantId, ct);
            await using var q = s2.Command("select id, request_hash from sales.sale where idempotency_key=@k");
            q.Parameters.AddWithValue("k", key);
            await using var r = await q.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct) && r.GetString(1) == hash) return ServiceResult<object>.Ok(new { id = r.GetGuid(0), replayed = true });
            return ServiceResult<object>.Fail("Idempotency-Key reused with a different request.", 409);
        }
    }

    static async Task<Guid> AccountAsync(TenantSession s, Guid tenantId, string code, CancellationToken ct)
    {
        var (name, type) = code switch
        {
            "1000" => ("Cash and Bank", "Asset"), "1200" => ("Accounts Receivable", "Asset"), "1400" => ("Inventory", "Asset"),
            "4000" => ("Sales Revenue", "Income"), "5000" => ("Cost of Goods Sold", "Expense"), _ => (code, "Asset")
        };
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
