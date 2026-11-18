using System.Security.Claims;
using Rbos.Api.Application;
using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record ItemTagReviewRequest(
    Guid ProductId, string? ProductName, string? Brand, string? Model, string? Barcode,
    string? BarcodeSymbology, string? Serial, string? Imei1, string? Imei2,
    decimal? Mrp, string? HsnCode);

public static class ItemTagCaptureEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/sales/item-tag-captures", async (
            HttpRequest request, ClaimsPrincipal principal, ItemTagCaptureService service, CancellationToken ct) =>
        {
            if (request.ContentLength is <= 0 or > ItemTagCaptureService.MaxFileBytes)
                return Fail(400, "Photo must be between 1 byte and 8 MB.");
            await using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            while (true)
            {
                var read = await request.Body.ReadAsync(chunk, ct);
                if (read == 0) break;
                if (buffer.Length + read > ItemTagCaptureService.MaxFileBytes)
                    return Fail(400, "Photo must be 8 MB or smaller.");
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
            if (buffer.Length == 0) return Fail(400, "Choose a photo before uploading.");
            var result = await service.UploadAsync(
                UserId(principal), request.Headers["Idempotency-Key"].ToString(),
                request.ContentType?.Split(';')[0].Trim().ToLowerInvariant(),
                request.Headers["X-RBOS-Detected-Barcode"].ToString(),
                request.Headers["X-RBOS-Detected-Symbology"].ToString(), buffer.ToArray(), ct);
            return Reply(result);
        }).RequireAuthorization();

        api.MapGet("/sales/item-tag-captures/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, ItemTagCaptureService service, CancellationToken ct) =>
            Reply(await service.GetAsync(UserId(principal), id, ct))).RequireAuthorization();

        api.MapGet("/sales/item-tag-captures/{id:guid}/image", async (
            Guid id, ClaimsPrincipal principal, HttpContext context, ItemTagCaptureService service, CancellationToken ct) =>
        {
            var image = await service.ImageAsync(UserId(principal), id, ct);
            if (image is null) return Fail(404, "Capture not found.");
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(image.Value.Bytes, image.Value.Mime);
        }).RequireAuthorization();

        api.MapPut("/sales/item-tag-captures/{id:guid}/review", async (
            Guid id, ItemTagReviewRequest request, ClaimsPrincipal principal, ItemTagCaptureService service, CancellationToken ct) =>
            Reply(await service.ReviewAsync(UserId(principal), id,
                new ItemTagReviewCommand(request.ProductId, request.ProductName, request.Brand, request.Model,
                    request.Barcode, request.BarcodeSymbology, request.Serial, request.Imei1, request.Imei2,
                    request.Mrp, request.HsnCode), ct))).RequireAuthorization();

        api.MapDelete("/sales/item-tag-captures/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, ItemTagCaptureService service, CancellationToken ct) =>
            Reply(await service.DeleteAsync(UserId(principal), id, ct))).RequireAuthorization();
    }

    static Guid UserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
