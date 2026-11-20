using System.Security.Claims;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
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
        const long maxMultipartBytes = 13L * 1024 * 1024;
        const long maxRequestBytes = 18L * 1024 * 1024;
        var extractInvoice = api.MapPost("/purchases/extract", async (
            HttpRequest request, ClaimsPrincipal p, InvoiceExtractionService x, CancellationToken ct) =>
        {
            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(ct);
                if (form.Files.Count == 0 || form.Files.Any(file => file.Name != "pages"))
                    return Fail(400, "Attach invoice page files using the 'pages' field.");
                if (form.Files.Count > InvoiceExtractionService.MaxPages)
                    return Fail(400, $"Upload at most {InvoiceExtractionService.MaxPages} pages of one invoice.");

                long totalBytes = 0;
                var pages = new List<(string? Mime, byte[] Bytes)>(form.Files.Count);
                foreach (var file in form.Files)
                {
                    if (file.Length is 0 or > InvoiceExtractionService.MaxFileBytes)
                        return Fail(413, "Each invoice page must be between 1 byte and 8 MB.");
                    totalBytes += file.Length;
                    if (totalBytes > 12L * 1024 * 1024)
                        return Fail(413, "The invoice pages together exceed 12 MB.");

                    await using var buffer = new MemoryStream((int)file.Length);
                    await file.CopyToAsync(buffer, ct);
                    if (buffer.Length != file.Length || buffer.Length > InvoiceExtractionService.MaxFileBytes)
                        return Fail(413, "An invoice page exceeded the allowed upload size.");
                    pages.Add((file.ContentType, buffer.ToArray()));
                }
                return Reply(await x.ExtractAsync(Uid(p), pages, ct));
            }

            // Compatibility for clients still sending JSON/base64 payloads.
            var legacy = await request.ReadFromJsonAsync<InvoiceUpload>(cancellationToken: ct);
            if (legacy is null) return Fail(400, "Invoice upload body is missing.");
            return Reply(await x.ExtractAsync(Uid(p),
                legacy.Pages is { Count: > 0 }
                    ? legacy.Pages.Select(page => (page.ContentType, page.Data)).ToList()
                    : [(legacy.ContentType, legacy.Data)],
                ct));
        }).RequireAuthorization();
        extractInvoice.WithMetadata(
            new RequestSizeLimitAttribute(maxRequestBytes),
            new RequestFormLimitsAttribute
            {
                MultipartBodyLengthLimit = maxMultipartBytes,
                ValueCountLimit = 8,
                KeyLengthLimit = 128,
                ValueLengthLimit = 1024
            });
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
