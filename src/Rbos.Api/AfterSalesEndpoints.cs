using System.Security.Claims;
using Rbos.Api.Application;
using Rbos.Domain;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record CollectionRequest(Guid CustomerId, DateOnly PaymentDate, decimal Amount, string? Method, string? Reference, List<BillAllocation>? Allocations = null);
public sealed record SaleReturnRequest(Guid SaleId, DateOnly ReturnDate, string? Reason, List<SaleReturnLineInput>? Lines, string? RefundMethod = null);
public sealed record CancelSaleRequest(string? Reason);
public sealed record PurchaseReturnRequest(Guid ReceiptId, DateOnly ReturnDate, string? Reason, List<ReturnLineInput>? Lines);

public static class AfterSalesEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/business-name", async (ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.BusinessAsync(Uid(p), ct))).RequireAuthorization();

        api.MapGet("/customers/{id:guid}/statement", async (Guid id, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.StatementAsync(Uid(p), id, ct))).RequireAuthorization();
		api.MapGet("/customers/{id:guid}/open-sales", async (Guid id, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.OpenSalesAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapGet("/customer-payments", async (Guid? customerId, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.PaymentsAsync(Uid(p), customerId, ct))).RequireAuthorization();
        api.MapGet("/customer-payments/{id:guid}", async (Guid id, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.PaymentAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapPost("/customer-payments", async (CollectionRequest m, HttpRequest req, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct)
            => Reply(await svc.CollectAsync(Uid(p), req.Headers["Idempotency-Key"].ToString(), new(m.CustomerId, m.PaymentDate, m.Amount, m.Method, m.Reference, m.Allocations), req.HttpContext.TraceIdentifier, ct))).RequireAuthorization();

        api.MapPost("/sales/{id:guid}/cancel", async (Guid id, CancelSaleRequest m, HttpRequest req, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct)
            => Reply(await svc.CancelSaleAsync(Uid(p), req.Headers["Idempotency-Key"].ToString(), id, m.Reason, req.HttpContext.TraceIdentifier, ct))).RequireAuthorization();
        api.MapGet("/sales/{id:guid}/returnable", async (Guid id, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.ReturnableSaleLinesAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapGet("/sale-returns", async (string? q, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.SaleReturnsAsync(Uid(p), q, ct))).RequireAuthorization();
        api.MapGet("/sale-returns/{id:guid}", async (Guid id, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.SaleReturnAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapPost("/sale-returns", async (SaleReturnRequest m, HttpRequest req, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct)
            => Reply(await svc.ReturnSaleAsync(Uid(p), req.Headers["Idempotency-Key"].ToString(), new(m.SaleId, m.ReturnDate, m.Reason, m.Lines, m.RefundMethod), req.HttpContext.TraceIdentifier, ct))).RequireAuthorization();

        api.MapGet("/purchases/{id:guid}", async (Guid id, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.PurchaseAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapGet("/purchase-returns", async (string? q, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.PurchaseReturnsAsync(Uid(p), q, ct))).RequireAuthorization();
        api.MapGet("/purchase-returns/{id:guid}", async (Guid id, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct) => Reply(await svc.PurchaseReturnAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapPost("/purchase-returns", async (PurchaseReturnRequest m, HttpRequest req, ClaimsPrincipal p, AfterSalesService svc, CancellationToken ct)
            => Reply(await svc.ReturnPurchaseAsync(Uid(p), req.Headers["Idempotency-Key"].ToString(), new(m.ReceiptId, m.ReturnDate, m.Reason, m.Lines), req.HttpContext.TraceIdentifier, ct))).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
