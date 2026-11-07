using System.Security.Claims;
using Rbos.Api.Application;

namespace Rbos.Api;

public sealed record MoneyAccountRequest(string Name, string Kind);
public sealed record ExpenseRequest(DateOnly Date, string Category, Guid MoneyAccountId, decimal Amount, string? Note);
public sealed record OwnerTxnRequest(string Kind, DateOnly Date, Guid MoneyAccountId, decimal Amount, string? Note);
public sealed record StockAdjustRequest(Guid ProductId, Guid LocationId, DateOnly Date, decimal Quantity, string Direction, string Reason, string? Note);
public sealed record LockRequest(DateOnly? LockedThrough, string? Reason);

public static class FinanceEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
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
        api.MapGet("/stock-adjustments", async (ClaimsPrincipal p, FinanceService s, CancellationToken ct) => Reply(await s.StockAdjustmentsAsync(Uid(p), ct))).RequireAuthorization();
        api.MapPost("/stock-adjustments", async (StockAdjustRequest m, HttpRequest r, ClaimsPrincipal p, FinanceService s, CancellationToken ct) =>
            Reply(await s.AdjustStockAsync(Uid(p), r.Headers["Idempotency-Key"].ToString(), new(m.ProductId, m.LocationId, m.Date, m.Quantity, m.Direction, m.Reason, m.Note), ct))).RequireAuthorization();
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

    static IResult Reply<T>(ServiceResult<T> r) => r.Error is null
        ? Results.Ok(r.Value)
        : r.Status == 409 ? Results.Conflict(new { error = r.Error })
        : r.Status == 404 ? Results.NotFound(new { error = r.Error })
        : Results.BadRequest(new { error = r.Error });
}
