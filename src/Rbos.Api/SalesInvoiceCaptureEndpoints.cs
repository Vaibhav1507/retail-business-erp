using System.Security.Claims;
using System.Security.Cryptography;
using Rbos.Api.Application;
using Rbos.Api.Persistence;
using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public static class SalesInvoiceCaptureEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/sales/invoice-image-extract", async (
            HttpRequest request,
            ClaimsPrincipal principal,
            TenantDb db,
            SalesInvoiceCaptureService service,
            SalesInvoiceEvidenceService evidence,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            if (request.ContentLength is <= 0 or > SalesInvoiceCaptureService.MaxFileBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var log = loggerFactory.CreateLogger("SalesInvoiceCaptureEndpoint");
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tenantId = await db.FindTenantAsync(userId, ct);
            if (tenantId is null)
                return Fail(409, "Set up a business before extracting a sales invoice.");

            await using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            try
            {
                while (true)
                {
                    var read = await request.Body.ReadAsync(chunk, ct);
                    if (read == 0) break;
                    if (buffer.Length + read > SalesInvoiceCaptureService.MaxFileBytes)
                        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                    await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
                }
                if (buffer.Length == 0) return Fail(400, "Choose an invoice photo before extracting.");

                var bytes = buffer.ToArray();
                try
                {
                    var mime = request.ContentType?.Split(';')[0].Trim().ToLowerInvariant();
                    if (!SalesInvoiceCaptureService.IsSupportedMime(mime))
                        return Fail(415, "Choose a JPEG, PNG, WebP photo or PDF invoice.");
                    if (!SalesInvoiceCaptureService.MatchesType(bytes, mime!))
                        return Fail(400, "The invoice file is corrupt or does not match its file type.");
                    Guid sourceDocumentId;
                    try
                    {
                        sourceDocumentId = await evidence.SaveAsync(
                            userId, tenantId.Value, mime!, bytes,
                            request.Headers["X-Source-File-Name"].ToString(), ct);
                    }
                    catch (InvalidDataException)
                    {
                        return Fail(400, "The uploaded file does not match its image or PDF format.");
                    }
                    catch (Exception error) when (error is Npgsql.PostgresException or Npgsql.NpgsqlException or InvalidOperationException or ArgumentException)
                    {
                        log.LogError("Could not retain a handwritten sales bill. Error type {ErrorType}.", error.GetType().Name);
                        return Fail(503, "The original bill could not be safely saved. No extraction was started; retry the upload.");
                    }

                    var result = await service.ExtractAsync(mime!, bytes, sourceDocumentId, ct);
                    return Reply(result);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(chunk);
                if (buffer.TryGetBuffer(out var internalBuffer))
                    CryptographicOperations.ZeroMemory(internalBuffer.AsSpan());
            }
        }).RequireAuthorization();

        api.MapPost("/sales/invoice-image-extract/{id:guid}/discard", async (
            Guid id,
            ClaimsPrincipal principal,
            TenantDb db,
            SalesInvoiceEvidenceService evidence,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tenantId = await db.FindTenantAsync(userId, ct);
            if (tenantId is null) return Results.NotFound();
            return Reply(await evidence.DiscardAsync(userId, tenantId.Value, id, ct));
        }).RequireAuthorization();
    }
}
