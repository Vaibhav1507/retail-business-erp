using System.Security.Claims;
using Rbos.Api.Application;
using Rbos.Domain;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record SupplierRequest(string? Name, string? Phone, string? Notes, bool? IsActive);
public sealed record ReceiptRequest(Guid SupplierId, Guid LocationId, DateOnly ReceiptDate, string? SupplierRef, List<PurchaseLineInput>? Lines, Guid? InvoiceFileId = null);
public sealed record InvoicePage(string? ContentType, string? Data);
public sealed record InvoiceUpload(string? ContentType, string? Data, List<InvoicePage>? Pages = null);
public sealed record PaymentRequest(Guid SupplierId, DateOnly PaymentDate, decimal Amount, string? Method, string? Reference, List<BillAllocation>? Allocations = null);

public static class ProcurementEndpoints
{
	public static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/suppliers/{id:guid}/statement", async (Guid id, ClaimsPrincipal p, ProcurementService svc, CancellationToken ct) => Reply(await svc.SupplierStatementAsync(Uid(p), id, ct))).RequireAuthorization();
		api.MapGet("/suppliers/{id:guid}/open-bills", async (Guid id, ClaimsPrincipal p, ProcurementService svc, CancellationToken ct) => Reply(await svc.OpenBillsAsync(Uid(p), id, ct))).RequireAuthorization();
		api.MapGet("/suppliers", async (ClaimsPrincipal p, ProcurementService svc, CancellationToken ct) => Reply(await svc.SuppliersAsync(Uid(p), ct))).RequireAuthorization();
		api.MapPost("/suppliers", async (SupplierRequest m, ClaimsPrincipal p, ProcurementService svc, CancellationToken ct)
			=> Reply(await svc.CreateSupplierAsync(Uid(p), new(m.Name, m.Phone, m.Notes), ct))).RequireAuthorization();
		api.MapPut("/suppliers/{id:guid}", async (Guid id, SupplierRequest m, ClaimsPrincipal p, ProcurementService svc, CancellationToken ct)
			=> Reply(await svc.UpdateSupplierAsync(Uid(p), id, new(m.Name, m.Phone, m.Notes, m.IsActive ?? true), ct))).RequireAuthorization();

		api.MapGet("/purchases", async (Guid? supplierId, ClaimsPrincipal p, ProcurementService svc, CancellationToken ct) => Reply(await svc.ReceiptsAsync(Uid(p), supplierId, ct))).RequireAuthorization();
		api.MapPost("/purchases", async (ReceiptRequest m, HttpRequest req, ClaimsPrincipal p, ProcurementService svc, CancellationToken ct)
			=> Reply(await svc.ReceiveAsync(Uid(p), req.Headers["Idempotency-Key"].ToString(), new(m.SupplierId, m.LocationId, m.ReceiptDate, m.SupplierRef, m.Lines, m.InvoiceFileId), req.HttpContext.TraceIdentifier, ct))).RequireAuthorization();
		api.MapPost("/purchases/extract", async (InvoiceUpload m, ClaimsPrincipal p, InvoiceExtractionService x, CancellationToken ct)
			=> Reply(await x.ExtractAsync(Uid(p), m.Pages is { Count: > 0 } ? m.Pages.Select(g => (g.ContentType, g.Data)).ToList() : [(m.ContentType, m.Data)], ct))).RequireAuthorization();
		api.MapGet("/purchases/{id:guid}/invoice", async (Guid id, ClaimsPrincipal p, InvoiceExtractionService x, CancellationToken ct) =>
		{
			var f = await x.FileAsync(Uid(p), id, ct);
			return f is null ? Results.NotFound() : Results.File(f.Value.Bytes, f.Value.Mime, f.Value.Mime == "application/zip" ? "invoice-pages.zip" : null);
		}).RequireAuthorization();

		api.MapGet("/supplier-payments", async (Guid? supplierId, ClaimsPrincipal p, ProcurementService svc, CancellationToken ct) => Reply(await svc.PaymentsAsync(Uid(p), supplierId, ct))).RequireAuthorization();
		api.MapPost("/supplier-payments", async (PaymentRequest m, HttpRequest req, ClaimsPrincipal p, ProcurementService svc, CancellationToken ct)
			=> Reply(await svc.PayAsync(Uid(p), req.Headers["Idempotency-Key"].ToString(), new(m.SupplierId, m.PaymentDate, m.Amount, m.Method, m.Reference, m.Allocations), req.HttpContext.TraceIdentifier, ct))).RequireAuthorization();
	}

	static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
