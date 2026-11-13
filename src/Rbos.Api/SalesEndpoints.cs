using System.Security.Claims;
using Rbos.Api.Application;
using Rbos.Domain;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record CustomerRequest(string? Name, string? Phone, string? Notes, bool? IsActive, string? WhatsappConsentStatus = null);
public sealed record SaleRequest(Guid? CustomerId, Guid LocationId, DateOnly SaleDate, string? SaleType, decimal? AmountPaid, string? PayMethod, List<SaleLineInput>? Lines, List<SalePaymentInput>? Payments = null, decimal? GstRate = null, string? PlaceOfSupply = null);

public static class SalesEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/customers", async (string? q, ClaimsPrincipal p, SalesService svc, CancellationToken ct) => Reply(await svc.CustomersAsync(Uid(p), q, ct))).RequireAuthorization();
        api.MapPost("/customers", async (CustomerRequest m, ClaimsPrincipal p, SalesService svc, CancellationToken ct)
            => Reply(await svc.CreateCustomerAsync(Uid(p), new(m.Name, m.Phone, m.Notes, true, m.WhatsappConsentStatus ?? "Unknown"), ct))).RequireAuthorization();
        api.MapPut("/customers/{id:guid}", async (Guid id, CustomerRequest m, ClaimsPrincipal p, SalesService svc, CancellationToken ct)
            => Reply(await svc.UpdateCustomerAsync(Uid(p), id, new(m.Name, m.Phone, m.Notes, m.IsActive ?? true, m.WhatsappConsentStatus), ct))).RequireAuthorization();

        api.MapGet("/sales", async (string? q, Guid? customerId, ClaimsPrincipal p, SalesService svc, CancellationToken ct) => Reply(await svc.SalesAsync(Uid(p), q, customerId, ct))).RequireAuthorization();
        api.MapGet("/sales/{id:guid}", async (Guid id, ClaimsPrincipal p, SalesService svc, CancellationToken ct) => Reply(await svc.SaleAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapPost("/sales/{id:guid}/invoice/whatsapp/retry", async (Guid id, ClaimsPrincipal p, SalesService svc, CancellationToken ct) => Reply(await svc.RetryWhatsAppInvoiceAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapPost("/sales", async (SaleRequest m, HttpRequest req, ClaimsPrincipal p, SalesService svc, CancellationToken ct)
            => Reply(await svc.SellAsync(Uid(p), req.Headers["Idempotency-Key"].ToString(), new(m.CustomerId, m.LocationId, m.SaleDate, m.SaleType, m.AmountPaid, m.PayMethod, m.Lines, m.GstRate, m.Payments, m.PlaceOfSupply), req.HttpContext.TraceIdentifier, ct))).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
