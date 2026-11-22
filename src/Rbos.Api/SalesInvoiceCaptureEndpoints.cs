using System.Security.Claims;
using System.Security.Cryptography;
using Rbos.Api.Application;
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
            CancellationToken ct) =>
        {
            if (request.ContentLength is <= 0 or > SalesInvoiceCaptureService.MaxFileBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (await db.FindTenantAsync(userId, ct) is null)
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
                    var result = await service.ExtractAsync(mime, bytes, ct);
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
    }
}
