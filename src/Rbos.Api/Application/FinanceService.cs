using Rbos.Domain;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Rbos.Api.Persistence;
using static Rbos.Api.Application.SqlHelpers;

namespace Rbos.Api.Application;

public sealed record ExpenseCommand(DateOnly Date, string Category, Guid MoneyAccountId, decimal Amount, string? Note);
public sealed record OwnerTxnCommand(string Kind, DateOnly Date, Guid MoneyAccountId, decimal Amount, string? Note);
public sealed record StockAdjustCommand(Guid ProductId, Guid LocationId, DateOnly Date, decimal Quantity, string Direction, string Reason, string? Note);
public sealed record CashClosingCommand(Guid MoneyAccountId, DateOnly Date, decimal Counted, string? Note);
public sealed record LoanCommand(string Lender, DateOnly Date, Guid MoneyAccountId, decimal Principal, decimal InterestRate, string? Note);
public sealed record LoanPaymentCommand(DateOnly Date, Guid MoneyAccountId, decimal Principal, decimal Interest, string? Note);

/// <summary>Slice 6: expenses, cash/bank, capital/drawings, stock adjustment, period lock and financial reports.</summary>
public sealed class FinanceService(TenantDb db)
{
    public static readonly Dictionary<string, (string Code, string Name)> ExpenseCategories = new()
    {
        ["Rent"] = ("6010", "Rent"), ["Salaries"] = ("6020", "Salaries"), ["Utilities"] = ("6030", "Utilities"),
        ["Transport"] = ("6040", "Transport"), ["Marketing"] = ("6050", "Marketing"), ["Repairs"] = ("6060", "Repairs"), ["Other"] = ("6090", "Other Expenses")
    };
    static readonly HashSet<string> Reasons = ["Damage", "Theft", "Shortage", "WriteOff", "FoundOnCount", "OwnerConsumption", "Other"];

    async Task<(TenantSession? S, Guid Tenant, ServiceResult<object>? Err)> OpenAsync(Guid uid, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return (null, default, ServiceResult<object>.Fail("No business set up.", 409));
        return (await db.BeginAsync(uid, tid, ct), tid.Value, null);
    }

    // ---------- Money accounts ----------
    public async Task<ServiceResult<object>> MoneyAccountsAsync(Guid uid, CancellationToken ct)
    {
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _ = s!;
        await EnsureDefaultMoney(s!, t, ct);
        await s!.CommitAsync(ct);
        await using var s2 = (await OpenAsync(uid, ct)).S!;
        var rows = await Rows(s2, @"select m.id, m.name, m.kind, a.code,
            coalesce((select sum(l.debit-l.credit) from accounting.journal_line l where l.account_id=m.account_id),0)
            from accounting.money_account m join accounting.account a on a.id=m.account_id order by m.name", ct);
        return ServiceResult<object>.Ok(rows.Select(r => new { id = r[0], name = r[1], kind = r[2], code = r[3], balance = r[4] }).ToList());
    }

    public async Task<ServiceResult<object>> CreateMoneyAccountAsync(Guid uid, string name, string kind, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 80) return ServiceResult<object>.Fail("Name is required (max 80).");
        if (kind is not ("Cash" or "Bank" or "UPI")) return ServiceResult<object>.Fail("Kind must be Cash, Bank or UPI.");
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _ = s!;
        await EnsureDefaultMoney(s!, t, ct);
        var next = await Scalar<long?>(s!, "select coalesce(max(code::bigint),1000)+1 from accounting.account where code ~ '^10[0-9][0-9]$'", ct) ?? 1001;
        if (next > 1099) return ServiceResult<object>.Fail("Too many cash/bank accounts.");
        var acc = Guid.NewGuid(); var id = Guid.NewGuid();
        try
        {
            await Exec(s!, ct, "insert into accounting.account(tenant_id,id,code,name,type) values (@t,@i,@c,@n,'Asset')", ("t", t), ("i", acc), ("c", next.ToString(CultureInfo.InvariantCulture)), ("n", name));
            await Exec(s!, ct, "insert into accounting.money_account(tenant_id,id,account_id,name,kind) values (@t,@i,@a,@n,@k)", ("t", t), ("i", id), ("a", acc), ("n", name), ("k", kind));
            await s!.CommitAsync(ct);
        }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("An account with that name already exists.", 409); }
        return ServiceResult<object>.Ok(new { id });
    }

    static async Task EnsureDefaultMoney(TenantSession s, Guid t, CancellationToken ct)
    {
        var acc = await AccountAsync(s, t, "1000", ct);
        await Exec(s, ct, "insert into accounting.money_account(tenant_id,id,account_id,name,kind) values (@t,@i,@a,'Cash and Bank','Cash') on conflict do nothing", ("t", t), ("i", Guid.NewGuid()), ("a", acc));
    }

    public async Task<ServiceResult<object>> MoneyHistoryAsync(Guid uid, Guid moneyAccountId, CancellationToken ct)
    {
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _ = s!;
        var acct = await Scalar<Guid?>(s!, "select account_id from accounting.money_account where id=@i", ct, ("i", moneyAccountId));
        if (acct is null) return ServiceResult<object>.Fail("Unknown account.", 404);
        var rows = await Rows(s!, @"select e.effective_date, e.source_type, l.debit, l.credit,
            sum(l.debit-l.credit) over (order by e.effective_date, e.posted_at, l.id) from accounting.journal_line l
            join accounting.journal_entry e on e.id=l.entry_id where l.account_id=@a order by e.effective_date desc, e.posted_at desc, l.id desc limit 200", ct, ("a", acct));
        return ServiceResult<object>.Ok(rows.Select(r => new { date = r[0], source = r[1], @in = r[2], @out = r[3], balance = r[4] }).ToList());
    }

    // ---------- Expenses / owner ----------
    public async Task<ServiceResult<object>> ExpensesAsync(Guid uid, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var rows = await Rows(s!, @"select e.id, e.number, e.expense_date, c.name, m.name, e.amount, e.note from accounting.expense e
            join accounting.account c on c.id=e.category_account_id join accounting.account m on m.id=e.paid_from_account_id order by e.expense_date desc, e.number desc limit 100", ct);
        return ServiceResult<object>.Ok(rows.Select(r => new { id = r[0], number = r[1], date = r[2], category = r[3], paidFrom = r[4], amount = r[5], note = r[6] }).ToList());
    }

    public async Task<ServiceResult<object>> AddExpenseAsync(Guid uid, string? key, ExpenseCommand m, CancellationToken ct)
    {
        if (key is null || key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        if (!ExpenseCategories.TryGetValue(m.Category ?? "", out var cat)) return ServiceResult<object>.Fail("Choose a valid expense category.");
        if (m.Amount <= 0 || decimal.Round(m.Amount, 2) != m.Amount) return ServiceResult<object>.Fail("Amount must be positive with at most 2 decimals.");
        if ((m.Note?.Length ?? 0) > 200) return ServiceResult<object>.Fail("Note is too long.");
        if (m.Date > DateOnly.FromDateTime(DateTime.UtcNow)) return ServiceResult<object>.Fail("Date cannot be in the future.");
        var hash = Hash(string.Join('|', m.Date.ToString("O"), m.Category, m.MoneyAccountId, m.Amount.ToString(CultureInfo.InvariantCulture), m.Note));
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _ = s!;
        try
        {
            var prior = await Prior(s!, "accounting.expense", key, hash, ct); if (prior is not null) return prior;
            var money = await Scalar<Guid?>(s!, "select account_id from accounting.money_account where id=@i", ct, ("i", m.MoneyAccountId));
            if (money is null) return ServiceResult<object>.Fail("Unknown cash/bank account.");
            var catAcc = await AccountAsync(s!, t, cat.Code, ct, cat.Name, "Expense");
            var id = Guid.NewGuid();
            var entry = await Post(s!, t, m.Date, "Expense", id, [(catAcc, m.Amount, 0m), (money.Value, 0m, m.Amount)], ct);
            await Exec(s!, ct, "insert into accounting.expense(tenant_id,id,expense_date,category_account_id,paid_from_account_id,amount,note,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@d,@c,@p,@a,@n,@k,@h,@j,@u)",
                ("t", t), ("i", id), ("d", m.Date), ("c", catAcc), ("p", money.Value), ("a", m.Amount), ("n", Blank(m.Note)), ("k", key), ("h", hash), ("j", entry), ("u", uid));
            await s!.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id });
        }
        catch (PostgresException e) when (e.SqlState == "RB001") { return ServiceResult<object>.Fail(e.MessageText, 409); }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("Duplicate request.", 409); }
    }

    public async Task<ServiceResult<object>> OwnerTxnsAsync(Guid uid, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var rows = await Rows(s!, @"select o.id, o.kind, o.txn_date, a.name, o.amount, o.note from accounting.owner_transaction o
            join accounting.account a on a.id=o.money_account_id order by o.txn_date desc, o.number desc limit 100", ct);
        return ServiceResult<object>.Ok(rows.Select(r => new { id = r[0], kind = r[1], date = r[2], account = r[3], amount = r[4], note = r[5] }).ToList());
    }

    public async Task<ServiceResult<object>> AddOwnerTxnAsync(Guid uid, string? key, OwnerTxnCommand m, CancellationToken ct)
    {
        if (key is null || key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        if (m.Kind is not ("Capital" or "Drawing")) return ServiceResult<object>.Fail("Kind must be Capital or Drawing.");
        if (m.Amount <= 0 || decimal.Round(m.Amount, 2) != m.Amount) return ServiceResult<object>.Fail("Amount must be positive with at most 2 decimals.");
        if ((m.Note?.Length ?? 0) > 200) return ServiceResult<object>.Fail("Note is too long.");
        if (m.Date > DateOnly.FromDateTime(DateTime.UtcNow)) return ServiceResult<object>.Fail("Date cannot be in the future.");
        var hash = Hash(string.Join('|', m.Kind, m.Date.ToString("O"), m.MoneyAccountId, m.Amount.ToString(CultureInfo.InvariantCulture), m.Note));
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _ = s!;
        try
        {
            var prior = await Prior(s!, "accounting.owner_transaction", key, hash, ct); if (prior is not null) return prior;
            var money = await Scalar<Guid?>(s!, "select account_id from accounting.money_account where id=@i", ct, ("i", m.MoneyAccountId));
            if (money is null) return ServiceResult<object>.Fail("Unknown cash/bank account.");
            var id = Guid.NewGuid();
            Guid entry;
            if (m.Kind == "Capital")
            {
                var eq = await AccountAsync(s!, t, "3000", ct, "Owner Capital", "Equity");
                entry = await Post(s!, t, m.Date, "Capital", id, [(money.Value, m.Amount, 0m), (eq, 0m, m.Amount)], ct);
            }
            else
            {
                var bal = await Scalar<decimal?>(s!, "select coalesce(sum(debit-credit),0) from accounting.journal_line where account_id=@a", ct, ("a", money.Value)) ?? 0m;
                if (bal < m.Amount) return ServiceResult<object>.Fail("Not enough balance in that account for this drawing.");
                var dr = await AccountAsync(s!, t, "3100", ct, "Owner Drawings", "Equity");
                entry = await Post(s!, t, m.Date, "Drawing", id, [(dr, m.Amount, 0m), (money.Value, 0m, m.Amount)], ct);
            }
            await Exec(s!, ct, "insert into accounting.owner_transaction(tenant_id,id,kind,txn_date,money_account_id,amount,note,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@kd,@d,@a,@m,@n,@k,@h,@j,@u)",
                ("t", t), ("i", id), ("kd", m.Kind), ("d", m.Date), ("a", money.Value), ("m", m.Amount), ("n", Blank(m.Note)), ("k", key), ("h", hash), ("j", entry), ("u", uid));
            await s!.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id });
        }
        catch (PostgresException e) when (e.SqlState == "RB001") { return ServiceResult<object>.Fail(e.MessageText, 409); }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("Duplicate request.", 409); }
    }

    // ---------- Stock adjustment ----------
    public async Task<ServiceResult<object>> StockAdjustmentsAsync(Guid uid, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var rows = await Rows(s!, @"select a.id, a.number, a.adjust_date, p.name, a.quantity_delta, a.value_delta, a.reason, a.note from inventory.stock_adjustment a
            join catalog.product p on p.id=a.product_id order by a.adjust_date desc, a.number desc limit 100", ct);
        return ServiceResult<object>.Ok(rows.Select(r => new { id = r[0], number = r[1], date = r[2], product = r[3], quantity = r[4], value = r[5], reason = r[6], note = r[7] }).ToList());
    }

    public async Task<ServiceResult<object>> AdjustStockAsync(Guid uid, string? key, StockAdjustCommand m, CancellationToken ct)
    {
        if (key is null || key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        if (!Reasons.Contains(m.Reason ?? "")) return ServiceResult<object>.Fail("A valid reason is required.");
        if (m.Reason == "Other" && string.IsNullOrWhiteSpace(m.Note)) return ServiceResult<object>.Fail("A note is required when the reason is Other.");
        if (m.Direction is not ("Decrease" or "Increase")) return ServiceResult<object>.Fail("Direction must be Decrease or Increase.");
        if (m.Quantity <= 0 || decimal.Round(m.Quantity, 3) != m.Quantity) return ServiceResult<object>.Fail("Quantity must be positive with at most 3 decimals.");
        if (m.Reason == "FoundOnCount" && m.Direction != "Increase") return ServiceResult<object>.Fail("Found/count gain must be an increase.");
        if (m.Reason is not ("FoundOnCount" or "Other") && m.Direction != "Decrease") return ServiceResult<object>.Fail("This reason must be a decrease.");
        if ((m.Note?.Length ?? 0) > 200) return ServiceResult<object>.Fail("Note is too long.");
        if (m.Date > DateOnly.FromDateTime(DateTime.UtcNow)) return ServiceResult<object>.Fail("Date cannot be in the future.");
        var hash = Hash(string.Join('|', m.ProductId, m.LocationId, m.Date.ToString("O"), m.Quantity.ToString(CultureInfo.InvariantCulture), m.Direction, m.Reason, m.Note));
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _ = s!;
        try
        {
            var prior = await Prior(s!, "inventory.stock_adjustment", key, hash, ct); if (prior is not null) return prior;
            decimal tq = 0, tv = 0, wac = 0;
            await using (var q = s!.Command("select total_quantity,inventory_value,wac from inventory.item_cost where product_id=@p for update"))
            {
                q.Parameters.AddWithValue("p", m.ProductId);
                await using var r = await q.ExecuteReaderAsync(ct);
                if (await r.ReadAsync(ct)) { tq = r.GetDecimal(0); tv = r.GetDecimal(1); wac = r.GetDecimal(2); }
            }
            if (wac <= 0) return ServiceResult<object>.Fail("This product has no stock cost yet.");
            var onHand = await Scalar<decimal?>(s!, "select quantity from inventory.stock_balance where product_id=@p and location_id=@l for update", ct, ("p", m.ProductId), ("l", m.LocationId)) ?? 0m;
            var dec = m.Direction == "Decrease";
            if (dec && m.Quantity > onHand) return ServiceResult<object>.Fail("Cannot reduce below zero stock at this location.");
            var value = dec && m.Quantity == tq ? tv : decimal.Round(m.Quantity * wac, 2, MidpointRounding.AwayFromZero);
            if (value <= 0) return ServiceResult<object>.Fail("Adjustment value rounds to zero.");
            var signedQ = dec ? -m.Quantity : m.Quantity; var signedV = dec ? -value : value;
            var inv = await AccountAsync(s!, t, "1400", ct);
            Guid other;
            if (dec) other = m.Reason == "OwnerConsumption" ? await AccountAsync(s!, t, "3100", ct, "Owner Drawings", "Equity") : await AccountAsync(s!, t, "5100", ct, "Stock Write-off Loss", "Expense");
            else other = await AccountAsync(s!, t, "4900", ct, "Inventory Gain", "Income");
            var id = Guid.NewGuid();
            var entry = await Post(s!, t, m.Date, "StockAdjustment", id, dec ? [(other, value, 0m), (inv, 0m, value)] : [(inv, value, 0m), (other, 0m, value)], ct);
            await Exec(s!, ct, "insert into inventory.stock_movement(tenant_id,id,product_id,location_id,quantity_delta,unit_cost,value_delta,effective_date,source_type,source_id) values (@t,@i,@p,@l,@q,@c,@v,@d,'StockAdjustment',@s)",
                ("t", t), ("i", Guid.NewGuid()), ("p", m.ProductId), ("l", m.LocationId), ("q", signedQ), ("c", wac), ("v", signedV), ("d", m.Date), ("s", id));
            await Exec(s!, ct, "insert into inventory.stock_balance(tenant_id,product_id,location_id,quantity) values (@t,@p,@l,@q) on conflict (tenant_id,product_id,location_id) do update set quantity = inventory.stock_balance.quantity + @dq",
                ("t", t), ("p", m.ProductId), ("l", m.LocationId), ("q", Math.Max(signedQ, 0m)), ("dq", signedQ));
            var nq = tq + signedQ; var nv = tv + signedV;
            await Exec(s!, ct, "update inventory.item_cost set total_quantity=@q, inventory_value=@v, wac=@w where product_id=@p", ("q", nq), ("v", nv), ("w", nq == 0 ? wac : WeightedAverageCostCalculator.RoundWac(nv / nq)), ("p", m.ProductId));
            await Exec(s!, ct, "insert into inventory.stock_adjustment(tenant_id,id,product_id,location_id,adjust_date,quantity_delta,unit_cost,value_delta,reason,note,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@p,@l,@d,@q,@c,@v,@r,@n,@k,@h,@j,@u)",
                ("t", t), ("i", id), ("p", m.ProductId), ("l", m.LocationId), ("d", m.Date), ("q", signedQ), ("c", wac), ("v", signedV), ("r", m.Reason), ("n", Blank(m.Note)), ("k", key), ("h", hash), ("j", entry), ("u", uid));
            await s!.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id, value });
        }
        catch (PostgresException e) when (e.SqlState == "RB001") { return ServiceResult<object>.Fail(e.MessageText, 409); }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("Duplicate request.", 409); }
    }

    // ---------- Period lock ----------
    public async Task<ServiceResult<object>> LockAsync(Guid uid, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var d = await Scalar<DateTime?>(s!, "select locked_through::timestamp from accounting.period_lock", ct);
        var hist = await Rows(s!, "select changed_at, previous, new_value, reason from accounting.period_lock_history order by changed_at desc limit 10", ct);
        return ServiceResult<object>.Ok(new { lockedThrough = d is null ? null : DateOnly.FromDateTime(d.Value).ToString("O"), history = hist.Select(r => new { at = r[0], previous = r[1], value = r[2], reason = r[3] }).ToList() });
    }

    public async Task<ServiceResult<object>> SetLockAsync(Guid uid, DateOnly? lockedThrough, string? reason, CancellationToken ct)
    {
        if (lockedThrough > DateOnly.FromDateTime(DateTime.UtcNow)) return ServiceResult<object>.Fail("Cannot lock future dates.");
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _ = s!;
        var cur = await Scalar<DateTime?>(s!, "select locked_through::timestamp from accounting.period_lock", ct);
        DateOnly? prev = cur is null ? null : DateOnly.FromDateTime(cur.Value);
        var backwards = prev is not null && (lockedThrough is null || lockedThrough < prev);
        if (backwards && string.IsNullOrWhiteSpace(reason)) return ServiceResult<object>.Fail("A reason is required to move the lock date backward.");
        if ((reason?.Length ?? 0) > 200) return ServiceResult<object>.Fail("Reason is too long.");
        await Exec(s!, ct, "insert into accounting.period_lock(tenant_id,locked_through,updated_by) values (@t,@d,@u) on conflict (tenant_id) do update set locked_through=@d, updated_by=@u, updated_at=now()", ("t", t), ("d", lockedThrough), ("u", uid));
        await Exec(s!, ct, "insert into accounting.period_lock_history(tenant_id,id,previous,new_value,reason,changed_by) values (@t,@i,@p,@n,@r,@u)", ("t", t), ("i", Guid.NewGuid()), ("p", prev), ("n", lockedThrough), ("r", Blank(reason)), ("u", uid));
        await s!.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { lockedThrough });
    }

    // ---------- Reports ----------
    public async Task<ServiceResult<object>> TrialBalanceAsync(Guid uid, DateOnly asOf, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var rows = await Rows(s!, @"select a.id, a.code, a.name, a.type, coalesce(sum(l.debit),0), coalesce(sum(l.credit),0) from accounting.account a
            left join accounting.journal_line l on l.account_id=a.id and exists (select 1 from accounting.journal_entry e where e.id=l.entry_id and e.effective_date<=@d)
            group by a.id,a.code,a.name,a.type order by a.code", ct, ("d", asOf));
        var lines = rows.Where(r => (decimal)r[4] != 0 || (decimal)r[5] != 0).Select(r => { var net = (decimal)r[4] - (decimal)r[5]; return new { id = r[0], code = r[1], name = r[2], type = r[3], debit = net > 0 ? net : 0m, credit = net < 0 ? -net : 0m }; }).ToList();
        return ServiceResult<object>.Ok(new { asOf, lines, totalDebit = lines.Sum(l => l.debit), totalCredit = lines.Sum(l => l.credit) });
    }

    public async Task<ServiceResult<object>> LedgerAsync(Guid uid, Guid accountId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var info = await Rows(s!, "select code, name, type from accounting.account where id=@a", ct, ("a", accountId));
        if (info.Count == 0) return ServiceResult<object>.Fail("Unknown account.", 404);
        var opening = await Scalar<decimal?>(s!, "select coalesce(sum(l.debit-l.credit),0) from accounting.journal_line l join accounting.journal_entry e on e.id=l.entry_id where l.account_id=@a and e.effective_date<@f", ct, ("a", accountId), ("f", from)) ?? 0m;
        var rows = await Rows(s!, @"select e.effective_date, e.source_type, e.source_id, l.debit, l.credit from accounting.journal_line l join accounting.journal_entry e on e.id=l.entry_id
            where l.account_id=@a and e.effective_date between @f and @t order by e.effective_date, e.posted_at, l.id", ct, ("a", accountId), ("f", from), ("t", to));
        var run = opening;
        var lines = rows.Select(r => { run += (decimal)r[3] - (decimal)r[4]; return new { date = r[0], source = r[1], sourceId = r[2], debit = r[3], credit = r[4], balance = run }; }).ToList();
        return ServiceResult<object>.Ok(new { code = info[0][0], name = info[0][1], type = info[0][2], from, to, opening, lines, closing = run });
    }

    public async Task<ServiceResult<object>> ProfitLossAsync(Guid uid, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var rows = await Rows(s!, @"select a.code, a.name, a.type, coalesce(sum(l.credit-l.debit),0) from accounting.account a
            join accounting.journal_line l on l.account_id=a.id join accounting.journal_entry e on e.id=l.entry_id
            where a.type in ('Income','Expense') and e.effective_date between @f and @t group by a.code,a.name,a.type order by a.code", ct, ("f", from), ("t", to));
        var income = rows.Where(r => (string)r[2] == "Income").Select(r => new { code = r[0], name = r[1], amount = (decimal)r[3] }).ToList();
        var expense = rows.Where(r => (string)r[2] == "Expense").Select(r => new { code = r[0], name = r[1], amount = -(decimal)r[3] }).ToList();
        var cogs = expense.Where(e => (string)e.code == "5000").Sum(e => e.amount);
        var revenue = income.Sum(i => i.amount); var exp = expense.Sum(e => e.amount);
        return ServiceResult<object>.Ok(new { from, to, income, expense, totalIncome = revenue, totalExpense = exp, grossProfit = income.Where(i => (string)i.code == "4000").Sum(i => i.amount) - cogs, netProfit = revenue - exp });
    }

    public async Task<ServiceResult<object>> BalanceSheetAsync(Guid uid, DateOnly asOf, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var rows = await Rows(s!, @"select a.code, a.name, a.type, coalesce(sum(l.debit-l.credit),0) from accounting.account a
            join accounting.journal_line l on l.account_id=a.id join accounting.journal_entry e on e.id=l.entry_id
            where e.effective_date<=@d group by a.code,a.name,a.type order by a.code", ct, ("d", asOf));
        var assets = rows.Where(r => (string)r[2] == "Asset").Select(r => new { code = r[0], name = r[1], amount = (decimal)r[3] }).ToList();
        var liabilities = rows.Where(r => (string)r[2] == "Liability").Select(r => new { code = r[0], name = r[1], amount = -(decimal)r[3] }).ToList();
        var equity = rows.Where(r => (string)r[2] == "Equity").Select(r => new { code = r[0], name = r[1], amount = -(decimal)r[3] }).ToList();
        var result = -rows.Where(r => (string)r[2] is "Income" or "Expense").Sum(r => (decimal)r[3]);
        var ta = assets.Sum(a => a.amount); var tl = liabilities.Sum(a => a.amount); var te = equity.Sum(a => a.amount) + result;
        return ServiceResult<object>.Ok(new { asOf, assets, liabilities, equity, currentYearResult = result, totalAssets = ta, totalLiabilities = tl, totalEquity = te, balanced = ta == tl + te });
    }

    public Task<ServiceResult<object>> ReceivableAgeingAsync(Guid uid, DateOnly asOf, CancellationToken ct) => AgeingAsync(uid, asOf, true, ct);
    public Task<ServiceResult<object>> PayableAgeingAsync(Guid uid, DateOnly asOf, CancellationToken ct) => AgeingAsync(uid, asOf, false, ct);

    async Task<ServiceResult<object>> AgeingAsync(Guid uid, DateOnly asOf, bool receivable, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var docs = receivable
            ? await Rows(s!, "select c.id, c.name, s.sale_date, s.total-s.amount_paid from sales.sale s join sales.customer c on c.id=s.customer_id where s.sale_date<=@d and s.total-s.amount_paid>0 and not exists (select 1 from sales.sale_cancellation k where k.sale_id=s.id) order by s.sale_date, s.created_at", ct, ("d", asOf))
            : await Rows(s!, "select p.id, p.name, r.receipt_date, r.total from procurement.purchase_receipt r join procurement.supplier p on p.id=r.supplier_id where r.receipt_date<=@d order by r.receipt_date, r.created_at", ct, ("d", asOf));
        var credits = receivable
            ? await Rows(s!, "select customer_id, sum(x) from (select customer_id, amount x from sales.customer_payment where payment_date<=@d union all select customer_id, receivable_credit from sales.sale_return where return_date<=@d and customer_id is not null) q group by customer_id", ct, ("d", asOf))
            : await Rows(s!, "select supplier_id, sum(x) from (select supplier_id, amount x from procurement.supplier_payment where payment_date<=@d union all select supplier_id, total from procurement.purchase_return where return_date<=@d) q group by supplier_id", ct, ("d", asOf));
        var credit = credits.ToDictionary(r => (Guid)r[0], r => (decimal)r[1]);
        var parties = new List<object>(); decimal t0 = 0, t1 = 0, t2 = 3 * 0, t3 = 0;
        foreach (var g in docs.GroupBy(r => (Guid)r[0]))
        {
            var left = credit.GetValueOrDefault(g.Key); decimal b0 = 0, b1 = 0, b2 = 0, b3 = 0;
            foreach (var d in g)
            {
                var amt = (decimal)d[3]; var use = Math.Min(left, amt); left -= use; amt -= use;
                if (amt <= 0) continue;
                var age = asOf.DayNumber - ((DateOnly)d[2]).DayNumber;
                if (age <= 30) b0 += amt; else if (age <= 60) b1 += amt; else if (age <= 90) b2 += amt; else b3 += amt;
            }
            var total = b0 + b1 + b2 + b3; if (total <= 0) continue;
            t0 += b0; t1 += b1; t2 += b2; t3 += b3;
            parties.Add(new { id = g.Key, name = g.First()[1], current = b0, d31to60 = b1, d61to90 = b2, over90 = b3, total });
        }
        return ServiceResult<object>.Ok(new { asOf, parties, current = t0, d31to60 = t1, d61to90 = t2, over90 = t3, total = t0 + t1 + t2 + t3 });
    }

    public async Task<ServiceResult<object>> StockValuationAsync(Guid uid, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var rows = await Rows(s!, "select p.id, p.name, c.total_quantity, c.wac, c.inventory_value from inventory.item_cost c join catalog.product p on p.id=c.product_id where c.total_quantity>0 order by p.name", ct);
        var items = rows.Select(r => new { id = r[0], name = r[1], quantity = r[2], wac = r[3], value = r[4] }).ToList();
        return ServiceResult<object>.Ok(new { items, totalValue = rows.Sum(r => (decimal)r[4]) });
    }

    // ---------- Chart of accounts (A10) ----------
    static readonly Dictionary<string, (long From, long To)> CustomRanges = new() { ["Asset"] = (1600, 1999), ["Liability"] = (2600, 2999), ["Expense"] = (7000, 7999), ["Income"] = (8000, 8999) };

    public async Task<ServiceResult<object>> ChartAsync(Guid uid, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var rows = await Rows(s!, "select a.id, a.code, a.name, a.type, a.is_custom, coalesce(sum(l.debit-l.credit),0), a.is_active from accounting.account a left join accounting.journal_line l on l.account_id=a.id and l.tenant_id=a.tenant_id group by a.id, a.code, a.name, a.type, a.is_custom, a.is_active order by a.code", ct);
        return ServiceResult<object>.Ok(rows.Select(r => new { id = r[0], code = r[1], name = r[2], type = r[3], custom = r[4], balance = r[5], active = r[6] }).ToList());
    }

    public async Task<ServiceResult<object>> AddAccountAsync(Guid uid, string name, string type, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 80) return ServiceResult<object>.Fail("Name is required (max 80).");
        if (!CustomRanges.TryGetValue(type ?? "", out var range)) return ServiceResult<object>.Fail("Type must be Asset, Liability, Income or Expense.");
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _ = s!;
        try
        {
            var next = await Scalar<long?>(s!, "select coalesce(max(code::bigint), @f-1)+1 from accounting.account where code ~ '^[0-9]{4}$' and code::bigint between @f and @e", ct, ("f", range.From), ("e", range.To)) ?? range.From;
            if (next > range.To) return ServiceResult<object>.Fail("No free account codes left for this type.");
            var id = Guid.NewGuid(); var code = next.ToString(CultureInfo.InvariantCulture);
            await Exec(s!, ct, "insert into accounting.account(tenant_id,id,code,name,type,is_custom) values (@t,@i,@c,@n,@y,true)", ("t", t), ("i", id), ("c", code), ("n", name), ("y", type));
            await s!.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id, code });
        }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("That code was just taken. Try again.", 409); }
    }

    public async Task<ServiceResult<object>> RenameAccountAsync(Guid uid, Guid id, string name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 80) return ServiceResult<object>.Fail("Name is required (max 80).");
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var custom = await Scalar<bool?>(s!, "select is_custom from accounting.account where id=@i", ct, ("i", id));
        if (custom is null) return ServiceResult<object>.Fail("Unknown account.", 404);
        if (custom == false) return ServiceResult<object>.Fail("System accounts cannot be renamed.", 409);
        await Exec(s!, ct, "update accounting.account set name=@n where id=@i", ("n", name), ("i", id));
        await s!.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id });
    }

    public async Task<ServiceResult<object>> SetAccountActiveAsync(Guid uid, Guid id, bool active, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var custom = await Scalar<bool?>(s!, "select is_custom from accounting.account where id=@i", ct, ("i", id));
        if (custom is null) return ServiceResult<object>.Fail("Unknown account.", 404);
        if (custom == false) return ServiceResult<object>.Fail("System accounts cannot be deactivated.", 409);
        if (!active)
        {
            var balance = await Scalar<decimal?>(s!, "select coalesce(sum(debit-credit),0) from accounting.journal_line where account_id=@i", ct, ("i", id)) ?? 0m;
            if (balance != 0m) return ServiceResult<object>.Fail("Only an account with a zero balance can be deactivated.", 409);
        }
        await Exec(s!, ct, "update accounting.account set is_active=@a where id=@i", ("a", active), ("i", id));
        await s!.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id, active });
    }

    // ---------- Stock cost verification (A12) ----------
    /// <summary>Read-only: replays stock movements and compares them with item_cost and stock_balance. Lists only products that disagree.</summary>
    public async Task<ServiceResult<object>> StockVerificationAsync(Guid uid, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var rows = await Rows(s!, @"with movement as (
                select m.tenant_id, coalesce(a.canonical_product_id, m.product_id) as product_id,
                       sum(m.quantity_delta) as q, sum(m.value_delta) as v
                from inventory.stock_movement m
                left join catalog.product_alias a on a.tenant_id=m.tenant_id and a.alias_product_id=m.product_id
                group by m.tenant_id, coalesce(a.canonical_product_id, m.product_id)
            ), balance as (
                select tenant_id, product_id, sum(quantity) as q from inventory.stock_balance group by tenant_id, product_id
            )
            select p.id, p.name, c.total_quantity, c.inventory_value, c.wac,
                   coalesce(m.q,0), coalesce(m.v,0), coalesce(b.q,0)
            from catalog.product p
            left join inventory.item_cost c on c.tenant_id=p.tenant_id and c.product_id=p.id
            left join movement m on m.tenant_id=p.tenant_id and m.product_id=p.id
            left join balance b on b.tenant_id=p.tenant_id and b.product_id=p.id
            where p.is_active and (coalesce(c.total_quantity,0) <> coalesce(m.q,0)
                or coalesce(c.inventory_value,0) <> coalesce(m.v,0)
                or coalesce(c.total_quantity,0) <> coalesce(b.q,0))
            order by p.name limit 200", ct);
        var items = rows.Select(r => new { productId = r[0], name = r[1], costQuantity = r[2] is DBNull ? 0m : (decimal)r[2], costValue = r[3] is DBNull ? 0m : (decimal)r[3], wac = r[4] is DBNull ? 0m : (decimal)r[4], movementQuantity = (decimal)r[5], movementValue = (decimal)r[6], balanceQuantity = (decimal)r[7] }).ToList();
        var replay = await ReplayFlagsAsync(s!, ct); return ServiceResult<object>.Ok(new { ok = items.Count == 0 && replay.Count == 0, items, replay });
    }

    /// <summary>Replays each product's movements in order at the running average cost and reports the first issue per product.</summary>
    static async Task<List<object>> ReplayFlagsAsync(TenantSession s, CancellationToken ct)
    {
        var replay = new List<object>();
        var moves = await Rows(s, @"select coalesce(a.canonical_product_id, m.product_id) as product_id,
                    p.name, m.id, m.quantity_delta, m.value_delta
                from inventory.stock_movement m
                left join catalog.product_alias a on a.tenant_id=m.tenant_id and a.alias_product_id=m.product_id
                join catalog.product p on p.tenant_id=m.tenant_id and p.id=coalesce(a.canonical_product_id, m.product_id)
                order by coalesce(a.canonical_product_id, m.product_id), m.effective_date, m.occurred_at, m.id", ct);
        Guid? current = null; decimal qty = 0m, val = 0m; var flagged = false;
        foreach (var mv in moves)
        {
            var pid = (Guid)mv[0];
            if (current != pid) { current = pid; qty = 0m; val = 0m; flagged = false; }
            var dq = (decimal)mv[3]; var dv = (decimal)mv[4];
            if (dq < 0m && !flagged)
            {
                var expected = qty <= 0m ? 0m : -Money.Round(-dq * WeightedAverageCostCalculator.RoundWac(val / qty));
                if (qty + dq < 0m || expected != dv)
                {
                    flagged = true;
                    replay.Add(new { productId = pid, name = mv[1], movementId = mv[2], quantity = dq, recordedValue = dv, expectedValue = expected, runningQuantity = qty });
                }
            }
            qty += dq; val += dv;
        }
        return replay;
    }

    // ---------- Export audit (D9) ----------
    public async Task<ServiceResult<object>> RecordExportAsync(Guid uid, string report, int rows, CancellationToken ct)
    {
        report = (report ?? "").Trim();
        if (report.Length == 0 || report.Length > 60) return ServiceResult<object>.Fail("A report name up to 60 characters is required.");
        if (rows < 0) return ServiceResult<object>.Fail("Row count cannot be negative.");
        var (s, tenantId, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var id = Guid.NewGuid();
        await Exec(s!, ct, "insert into audit.audit_event(tenant_id,id,actor_user_id,action,entity_type,entity_id,summary,correlation_id) values (@t,@a,@u,'ReportExported','Report',@d,@s,@c)",
            ("t", tenantId), ("a", Guid.NewGuid()), ("u", uid), ("d", id), ("s", $"{report} exported as CSV ({rows} rows)"), ("c", Guid.NewGuid()));
        await s!.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id });
    }

    // ---------- Loans (A9) ----------
    public async Task<ServiceResult<object>> LoansAsync(Guid uid, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var rows = await Rows(s!, @"select l.id, l.number, l.lender, l.principal, l.start_date, l.interest_rate, l.note,
            coalesce((select sum(p.principal_paid) from accounting.loan_payment p where p.loan_id=l.id),0),
            coalesce((select sum(p.interest_paid) from accounting.loan_payment p where p.loan_id=l.id),0)
            from accounting.loan l order by l.start_date desc, l.number desc limit 200", ct);
        return ServiceResult<object>.Ok(rows.Select(r => new { id = r[0], number = r[1], lender = r[2], principal = r[3], startDate = r[4], interestRate = r[5], note = r[6], principalPaid = r[7], interestPaid = r[8], outstanding = (decimal)r[3] - (decimal)r[7] }).ToList());
    }

    /// <summary>Read-only repayment schedule: payments so far plus simple interest accrued since the last payment (or start date).</summary>
    public async Task<ServiceResult<object>> LoanScheduleAsync(Guid uid, Guid loanId, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var __ = s!;
        var loan = await Rows(s!, "select lender, principal, start_date, interest_rate from accounting.loan where id=@i", ct, ("i", loanId));
        if (loan.Count == 0) return ServiceResult<object>.Fail("Unknown loan.", 404);
        var principal = (decimal)loan[0][1]; var start = (DateOnly)loan[0][2]; var rate = (decimal)loan[0][3];
        var pays = await Rows(s!, "select pay_date, principal_paid, interest_paid from accounting.loan_payment where loan_id=@i order by pay_date, created_at", ct, ("i", loanId));
        var payments = pays.Select(r => new { date = r[0], principalPaid = (decimal)r[1], interestPaid = (decimal)r[2] }).ToList();
        var outstanding = principal - payments.Sum(p => p.principalPaid);
        var lastDate = pays.Count == 0 ? start : (DateOnly)pays[^1][0];
        var days = Math.Max(0, DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - lastDate.DayNumber);
        var accrued = decimal.Round(outstanding * rate / 100m * days / 365m, 2);
        return ServiceResult<object>.Ok(new { lender = loan[0][0], principal, interestRate = rate, outstanding, accruedInterest = accrued, accruedSince = lastDate, payments });
    }

    public async Task<ServiceResult<object>> AddLoanAsync(Guid uid, string? key, LoanCommand m, CancellationToken ct)
    {
        if (key is null || key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        var lender = (m.Lender ?? "").Trim();
        if (lender.Length is < 1 or > 100) return ServiceResult<object>.Fail("Lender name is required (max 100).");
        if (m.Principal <= 0 || decimal.Round(m.Principal, 2) != m.Principal) return ServiceResult<object>.Fail("Amount must be positive with at most 2 decimals.");
        if (m.InterestRate < 0 || m.InterestRate > 100 || decimal.Round(m.InterestRate, 2) != m.InterestRate) return ServiceResult<object>.Fail("Interest rate must be 0-100 with at most 2 decimals.");
        if ((m.Note?.Length ?? 0) > 200) return ServiceResult<object>.Fail("Note is too long.");
        if (m.Date > DateOnly.FromDateTime(DateTime.UtcNow)) return ServiceResult<object>.Fail("Date cannot be in the future.");
        var hash = Hash(string.Join('|', lender, m.Date.ToString("O"), m.MoneyAccountId, m.Principal.ToString(CultureInfo.InvariantCulture), m.InterestRate.ToString(CultureInfo.InvariantCulture), m.Note));
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _ = s!;
        try
        {
            var prior = await Prior(s!, "accounting.loan", key, hash, ct); if (prior is not null) return prior;
            var money = await Scalar<Guid?>(s!, "select account_id from accounting.money_account where id=@i", ct, ("i", m.MoneyAccountId));
            if (money is null) return ServiceResult<object>.Fail("Unknown cash/bank account.");
            var id = Guid.NewGuid();
            var liability = await AccountAsync(s!, t, "2500", ct, "Loans Payable", "Liability");
            var entry = await Post(s!, t, m.Date, "LoanReceived", id, [(money.Value, m.Principal, 0m), (liability, 0m, m.Principal)], ct);
            await Exec(s!, ct, "insert into accounting.loan(tenant_id,id,lender,principal,start_date,interest_rate,money_account_id,note,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@l,@p,@d,@r,@a,@n,@k,@h,@j,@u)",
                ("t", t), ("i", id), ("l", lender), ("p", m.Principal), ("d", m.Date), ("r", m.InterestRate), ("a", m.MoneyAccountId), ("n", Blank(m.Note)), ("k", key), ("h", hash), ("j", entry), ("u", uid));
            await s!.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id });
        }
        catch (PostgresException e) when (e.SqlState == "RB001") { return ServiceResult<object>.Fail(e.MessageText, 409); }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("Duplicate request.", 409); }
    }

    public async Task<ServiceResult<object>> RepayLoanAsync(Guid uid, Guid loanId, string? key, LoanPaymentCommand m, CancellationToken ct)
    {
        if (key is null || key.Length is < 8 or > 100) return ServiceResult<object>.Fail("Idempotency-Key header (8-100 chars) required.");
        if (m.Principal < 0 || m.Interest < 0 || decimal.Round(m.Principal, 2) != m.Principal || decimal.Round(m.Interest, 2) != m.Interest || m.Principal + m.Interest <= 0)
            return ServiceResult<object>.Fail("Enter a principal and/or interest amount (at most 2 decimals).");
        if ((m.Note?.Length ?? 0) > 200) return ServiceResult<object>.Fail("Note is too long.");
        if (m.Date > DateOnly.FromDateTime(DateTime.UtcNow)) return ServiceResult<object>.Fail("Date cannot be in the future.");
        var hash = Hash(string.Join('|', loanId, m.Date.ToString("O"), m.MoneyAccountId, m.Principal.ToString(CultureInfo.InvariantCulture), m.Interest.ToString(CultureInfo.InvariantCulture), m.Note));
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _ = s!;
        try
        {
            var prior = await Prior(s!, "accounting.loan_payment", key, hash, ct); if (prior is not null) return prior;
            await Exec(s!, ct, "select pg_advisory_xact_lock(hashtextextended(@l::text, 0))", ("l", loanId));
            var loan = await Scalar<decimal?>(s!, "select principal - coalesce((select sum(principal_paid) from accounting.loan_payment where loan_id=@l),0) from accounting.loan where id=@l", ct, ("l", loanId));
            if (loan is null) return ServiceResult<object>.Fail("Unknown loan.", 404);
            if (m.Principal > loan.Value) return ServiceResult<object>.Fail("Principal repayment is more than the outstanding loan.");
            var money = await Scalar<Guid?>(s!, "select account_id from accounting.money_account where id=@i", ct, ("i", m.MoneyAccountId));
            if (money is null) return ServiceResult<object>.Fail("Unknown cash/bank account.");
            var bal = await Scalar<decimal?>(s!, "select coalesce(sum(debit-credit),0) from accounting.journal_line where account_id=@a", ct, ("a", money.Value)) ?? 0m;
            if (bal < m.Principal + m.Interest) return ServiceResult<object>.Fail("Not enough balance in that account for this repayment.");
            var id = Guid.NewGuid();
            var liability = await AccountAsync(s!, t, "2500", ct, "Loans Payable", "Liability");
            var lines = new List<(Guid Account, decimal Dr, decimal Cr)>();
            if (m.Principal > 0) lines.Add((liability, m.Principal, 0m));
            if (m.Interest > 0) lines.Add((await AccountAsync(s!, t, "6100", ct, "Interest Expense", "Expense"), m.Interest, 0m));
            lines.Add((money.Value, 0m, m.Principal + m.Interest));
            var entry = await Post(s!, t, m.Date, "LoanRepayment", id, [.. lines], ct);
            await Exec(s!, ct, "insert into accounting.loan_payment(tenant_id,id,loan_id,pay_date,principal_paid,interest_paid,money_account_id,note,idempotency_key,request_hash,journal_entry_id,created_by) values (@t,@i,@l,@d,@p,@x,@a,@n,@k,@h,@j,@u)",
                ("t", t), ("i", id), ("l", loanId), ("d", m.Date), ("p", m.Principal), ("x", m.Interest), ("a", m.MoneyAccountId), ("n", Blank(m.Note)), ("k", key), ("h", hash), ("j", entry), ("u", uid));
            await s!.CommitAsync(ct);
            return ServiceResult<object>.Ok(new { id });
        }
        catch (PostgresException e) when (e.SqlState == "RB001") { return ServiceResult<object>.Fail(e.MessageText, 409); }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("Duplicate request.", 409); }
    }

    // ---------- helpers ----------
    static async Task<Guid> Post(TenantSession s, Guid t, DateOnly date, string source, Guid sourceId, (Guid Account, decimal Dr, decimal Cr)[] lines, CancellationToken ct)
    {
        var entry = Guid.NewGuid();
        await Exec(s, ct, "insert into accounting.journal_entry(tenant_id,id,effective_date,source_type,source_id) values (@t,@e,@d,@y,@s)", ("t", t), ("e", entry), ("d", date), ("y", source), ("s", sourceId));
        foreach (var l in lines)
            await Exec(s, ct, "insert into accounting.journal_line(tenant_id,id,entry_id,account_id,debit,credit) values (@t,@i,@e,@a,@d,@c)", ("t", t), ("i", Guid.NewGuid()), ("e", entry), ("a", l.Account), ("d", l.Dr), ("c", l.Cr));
        return entry;
    }

    static async Task<Guid> AccountAsync(TenantSession s, Guid t, string code, CancellationToken ct, string? name = null, string? type = null)
    {
        var (n, ty) = code switch
        {
            "1000" => ("Cash and Bank", "Asset"), "1200" => ("Accounts Receivable", "Asset"), "1400" => ("Inventory", "Asset"), "2000" => ("Accounts Payable", "Liability"),
            "4000" => ("Sales Revenue", "Income"), "5000" => ("Cost of Goods Sold", "Expense"), _ => (name ?? code, type ?? "Asset")
        };
        await Exec(s, ct, "insert into accounting.account(tenant_id,id,code,name,type) values (@t,@i,@c,@n,@y) on conflict (tenant_id,code) do nothing", ("t", t), ("i", Guid.NewGuid()), ("c", code), ("n", n), ("y", ty));
        return (await Scalar<Guid?>(s, "select id from accounting.account where code=@c", ct, ("c", code)))!.Value;
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

    // ---------- Cash day closing (A6) ----------
    const string CashBalanceSql = "select coalesce(sum(l.debit-l.credit),0) from accounting.journal_line l join accounting.journal_entry e on e.id=l.entry_id where l.account_id=@a and e.effective_date<=@d";

    public async Task<ServiceResult<object>> CashClosingsAsync(Guid uid, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        var rows = await Rows(s!, @"select c.id, c.closing_date, m.name, c.expected, c.counted, c.difference, c.note from accounting.cash_closing c
            join accounting.money_account m on m.id=c.money_account_id order by c.closing_date desc, m.name limit 200", ct);
        return ServiceResult<object>.Ok(rows.Select(r => new { id = r[0], date = r[1], account = r[2], expected = r[3], counted = r[4], difference = r[5], note = r[6] }).ToList());
    }

    public async Task<ServiceResult<object>> CashClosingPreviewAsync(Guid uid, Guid moneyAccountId, DateOnly date, CancellationToken ct)
    {
        var (s, _, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        var acc = await Scalar<Guid?>(s!, "select account_id from accounting.money_account where id=@i", ct, ("i", moneyAccountId));
        if (acc is null) return ServiceResult<object>.Fail("Unknown cash/bank account.", 404);
        var expected = await Scalar<decimal?>(s!, CashBalanceSql, ct, ("a", acc.Value), ("d", date)) ?? 0m;
        return ServiceResult<object>.Ok(new { date, expected });
    }

    public async Task<ServiceResult<object>> CloseCashAsync(Guid uid, CashClosingCommand m, CancellationToken ct)
    {
        if (m.Counted < 0 || decimal.Round(m.Counted, 2) != m.Counted) return ServiceResult<object>.Fail("Counted cash must be zero or more with at most 2 decimals.");
        if ((m.Note?.Length ?? 0) > 200) return ServiceResult<object>.Fail("Note is too long.");
        if (m.Date > DateOnly.FromDateTime(DateTime.UtcNow)) return ServiceResult<object>.Fail("Date cannot be in the future.");
        var (s, t, err) = await OpenAsync(uid, ct); if (err is not null) return err;
        await using var _s = s!;
        var acc = await Scalar<Guid?>(s!, "select account_id from accounting.money_account where id=@i", ct, ("i", m.MoneyAccountId));
        if (acc is null) return ServiceResult<object>.Fail("Unknown cash/bank account.", 404);
        if (await Scalar<long?>(s!, "select count(*) from accounting.cash_closing where money_account_id=@i and closing_date=@d", ct, ("i", m.MoneyAccountId), ("d", m.Date)) > 0)
            return ServiceResult<object>.Fail("This account is already closed for that day.", 409);
        var expected = await Scalar<decimal?>(s!, CashBalanceSql, ct, ("a", acc.Value), ("d", m.Date)) ?? 0m;
        var diff = m.Counted - expected;
        await Exec(s!, ct, "insert into accounting.cash_closing(tenant_id,id,money_account_id,closing_date,expected,counted,difference,note,created_by) values (@t,@i,@a,@d,@e,@c,@df,@n,@u)",
            ("t", t), ("i", Guid.NewGuid()), ("a", m.MoneyAccountId), ("d", m.Date), ("e", expected), ("c", m.Counted), ("df", diff), ("n", Blank(m.Note)), ("u", uid));
        await s!.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { date = m.Date, expected, counted = m.Counted, difference = diff });
    }
}
