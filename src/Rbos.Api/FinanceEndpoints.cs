using System.Security.Claims;
using Npgsql;
using Rbos.Api.Application;
using Rbos.Api.Persistence;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record CashClosingRequest(Guid MoneyAccountId, DateOnly Date, decimal Counted, string? Note);
public sealed record MoneyAccountRequest(string Name, string Kind);public sealed record ExpenseRequest(DateOnly Date, string Category, Guid MoneyAccountId, decimal Amount, string? Note);
public sealed record OwnerTxnRequest(string Kind, DateOnly Date, Guid MoneyAccountId, decimal Amount, string? Note);
public sealed record StockAdjustRequest(Guid ProductId, Guid LocationId, DateOnly Date, decimal Quantity, string Direction, string Reason, string? Note);
public sealed record AccountRequest(string Name, string Type);
public sealed record AccountActiveRequest(bool Active);
public sealed record ExportAuditRequest(string Report, int Rows);
public sealed record LoanRequest(string Lender, DateOnly Date, Guid MoneyAccountId, decimal Principal, decimal InterestRate, string? Note);
public sealed record LoanPaymentRequest(DateOnly Date, Guid MoneyAccountId, decimal Principal, decimal Interest, string? Note);
public sealed record LockRequest(DateOnly? LockedThrough, string? Reason);

public static class FinanceEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
        api.MapGet("/accounts", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.ChartAsync(Uid(p), ct))).RequireAuthorization();
        api.MapPost("/accounts", async (AccountRequest m, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.AddAccountAsync(Uid(p), m.Name, m.Type, ct))).RequireAuthorization();
        api.MapPut("/accounts/{id:guid}/active", async (Guid id, AccountActiveRequest m, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.SetAccountActiveAsync(Uid(p), id, m.Active, ct))).RequireAuthorization();
        api.MapPut("/accounts/{id:guid}", async (Guid id, AccountRequest m, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.RenameAccountAsync(Uid(p), id, m.Name, ct))).RequireAuthorization();
        api.MapPost("/exports/audit", async (ExportAuditRequest m, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.RecordExportAsync(Uid(p), m.Report, m.Rows, ct))).RequireAuthorization();
        api.MapGet("/reports/stock-verification", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.StockVerificationAsync(Uid(p), ct))).RequireAuthorization();
        api.MapGet("/money-accounts", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.MoneyAccountsAsync(Uid(p), ct))).RequireAuthorization();
        api.MapPost("/money-accounts", async (MoneyAccountRequest m, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.CreateMoneyAccountAsync(Uid(p), m.Name, m.Kind, ct))).RequireAuthorization();
        api.MapGet("/money-accounts/{id:guid}/history", async (Guid id, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.MoneyHistoryAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapGet("/expense-categories", () => Results.Ok(FinanceService.ExpenseCategories.Keys)).RequireAuthorization();
        api.MapGet("/expenses", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.ExpensesAsync(Uid(p), ct))).RequireAuthorization();
        api.MapPost("/expenses", async (ExpenseRequest m, HttpRequest r, ClaimsPrincipal p, FinanceService s, CancellationToken ct) =>
            Reply(await s.AddExpenseAsync(Uid(p), r.Headers["Idempotency-Key"].ToString(), new(m.Date, m.Category, m.MoneyAccountId, m.Amount, m.Note), ct))).RequireAuthorization();
        api.MapGet("/owner-transactions", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.OwnerTxnsAsync(Uid(p), ct))).RequireAuthorization();
        api.MapPost("/owner-transactions", async (OwnerTxnRequest m, HttpRequest r, ClaimsPrincipal p, FinanceService s, CancellationToken ct) =>
            Reply(await s.AddOwnerTxnAsync(Uid(p), r.Headers["Idempotency-Key"].ToString(), new(m.Kind, m.Date, m.MoneyAccountId, m.Amount, m.Note), ct))).RequireAuthorization();
        api.MapGet("/loans", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.LoansAsync(Uid(p), ct))).RequireAuthorization();
        api.MapPost("/loans", async (LoanRequest m, HttpRequest r, ClaimsPrincipal p, FinanceService s, CancellationToken ct) =>
            Reply(await s.AddLoanAsync(Uid(p), r.Headers["Idempotency-Key"].ToString(), new(m.Lender, m.Date, m.MoneyAccountId, m.Principal, m.InterestRate, m.Note), ct))).RequireAuthorization();
        api.MapGet("/loans/{id:guid}/schedule", async (Guid id, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.LoanScheduleAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapPost("/loans/{id:guid}/repayments", async (Guid id, LoanPaymentRequest m, HttpRequest r, ClaimsPrincipal p, FinanceService s, CancellationToken ct) =>
            Reply(await s.RepayLoanAsync(Uid(p), id, r.Headers["Idempotency-Key"].ToString(), new(m.Date, m.MoneyAccountId, m.Principal, m.Interest, m.Note), ct))).RequireAuthorization();
        api.MapGet("/stock-adjustments", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.StockAdjustmentsAsync(Uid(p), ct))).RequireAuthorization();
        api.MapPost("/stock-adjustments", async (StockAdjustRequest m, HttpRequest r, ClaimsPrincipal p, FinanceService s, CancellationToken ct) =>
            Reply(await s.AdjustStockAsync(Uid(p), r.Headers["Idempotency-Key"].ToString(), new(m.ProductId, m.LocationId, m.Date, m.Quantity, m.Direction, m.Reason, m.Note), ct))).RequireAuthorization();
        api.MapGet("/cash-closings", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.CashClosingsAsync(Uid(p), ct))).RequireAuthorization();
        api.MapGet("/cash-closings/preview", async (Guid moneyAccountId, DateOnly? date, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.CashClosingPreviewAsync(Uid(p), moneyAccountId, date ?? Today(), ct))).RequireAuthorization();
        api.MapPost("/cash-closings", async (CashClosingRequest m, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.CloseCashAsync(Uid(p), new(m.MoneyAccountId, m.Date, m.Counted, m.Note), ct))).RequireAuthorization();
        api.MapGet("/period-lock", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.LockAsync(Uid(p), ct))).RequireAuthorization();
        api.MapPut("/period-lock", async (LockRequest m, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.SetLockAsync(Uid(p), m.LockedThrough, m.Reason, ct))).RequireAuthorization();

        api.MapGet("/reports/trial-balance", async (DateOnly? asOf, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.TrialBalanceAsync(Uid(p), asOf ?? Today(), ct))).RequireAuthorization();
        api.MapGet("/reports/ledger/{accountId:guid}", async (Guid accountId, DateOnly? from, DateOnly? to, ClaimsPrincipal p, FinanceService s, CancellationToken ct) =>
            Reply(await s.LedgerAsync(Uid(p), accountId, from ?? new DateOnly(Today().Year, 1, 1), to ?? Today(), ct))).RequireAuthorization();
        api.MapGet("/reports/profit-loss", async (DateOnly? from, DateOnly? to, ClaimsPrincipal p, FinanceService s, CancellationToken ct) =>
            Reply(await s.ProfitLossAsync(Uid(p), from ?? new DateOnly(Today().Year, 1, 1), to ?? Today(), ct))).RequireAuthorization();
        api.MapGet("/reports/balance-sheet", async (DateOnly? asOf, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.BalanceSheetAsync(Uid(p), asOf ?? Today(), ct))).RequireAuthorization();
        api.MapGet("/reports/receivable-ageing", async (DateOnly? asOf, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.ReceivableAgeingAsync(Uid(p), asOf ?? Today(), ct))).RequireAuthorization();
        api.MapGet("/reports/payable-ageing", async (DateOnly? asOf, ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.PayableAgeingAsync(Uid(p), asOf ?? Today(), ct))).RequireAuthorization();
        api.MapGet("/reports/stock-valuation", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.StockValuationAsync(Uid(p), ct))).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record BankMatchRequest(Guid MoneyAccountId, List<Guid> LineIds, bool Cleared);

/// <summary>A7: bank reconciliation. Marks ledger lines of a money account as matched to the bank statement.</summary>
public static class BankMatchEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/reconciliation", async (Guid moneyAccountId, DateOnly? asOf, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            var cutoff = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
            await using var s = await db.BeginAsync(uid, tid, ct);
            await using var c = s.Command(@"select l.id, e.effective_date, e.source_type, l.debit, l.credit, (bc.line_id is not null)
                from accounting.money_account m
                join accounting.journal_line l on l.account_id = m.account_id and l.tenant_id = m.tenant_id
                join accounting.journal_entry e on e.id = l.entry_id and e.tenant_id = l.tenant_id
                left join accounting.bank_clearing bc on bc.line_id = l.id and bc.tenant_id = l.tenant_id
                where m.id = @m and e.effective_date <= @d
                order by e.effective_date desc, e.posted_at desc limit 500");
            c.Parameters.AddWithValue("m", moneyAccountId); c.Parameters.AddWithValue("d", cutoff);
            var lines = new List<object>();
            decimal book = 0, cleared = 0;
            await using (var r = await c.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                {
                    var amount = r.GetDecimal(3) - r.GetDecimal(4);
                    var isCleared = r.GetBoolean(5);
                    book += amount; if (isCleared) cleared += amount;
                    lines.Add(new { id = r.GetGuid(0), date = DateOnly.FromDateTime(r.GetDateTime(1)), source = r.GetString(2), amount, cleared = isCleared });
                }
            return Results.Ok(new { lines, bookBalance = book, clearedBalance = cleared, truncated = lines.Count == 500 });
        });

        api.MapPost("/reconciliation/clear", async (BankMatchRequest m, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            if (m.LineIds is not { Count: > 0 and <= 500 }) return Fail(400, "Select between 1 and 500 lines.");
            try
            {
                await using var s = await db.BeginAsync(uid, tid, ct);
                foreach (var id in m.LineIds.Distinct())
                {
                    await using var c = s.Command(m.Cleared
                        ? @"insert into accounting.bank_clearing(tenant_id,line_id,money_account_id,cleared_by)
                            select @t, l.id, ma.id, @u from accounting.journal_line l join accounting.money_account ma on ma.account_id = l.account_id and ma.tenant_id = l.tenant_id
                            where l.id = @l and ma.id = @m on conflict do nothing"
                        : "delete from accounting.bank_clearing where line_id = @l and money_account_id = @m");
                    c.Parameters.AddWithValue("l", id); c.Parameters.AddWithValue("m", m.MoneyAccountId);
                    if (m.Cleared) { c.Parameters.AddWithValue("t", tid.Value); c.Parameters.AddWithValue("u", uid); }
                    await c.ExecuteNonQueryAsync(ct);
                }
                await s.CommitAsync(ct);
                return Results.Ok(new { updated = m.LineIds.Distinct().Count() });
            }
            catch (PostgresException e) when (e.SqlState == "23503") { return Fail(404, "Unknown account or line."); }
        });
    }

    static async Task<(Guid Uid, Guid? Tid)> Ctx(ClaimsPrincipal p, TenantDb db, CancellationToken ct)
    {
        var uid = Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return (uid, await db.FindTenantAsync(uid, ct));
    }
}
