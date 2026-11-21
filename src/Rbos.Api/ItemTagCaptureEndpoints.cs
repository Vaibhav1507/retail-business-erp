using System.Security.Claims;
using Rbos.Api.Application;
using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public static class ItemTagCaptureEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/sales/item-tag-scan", async (
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
            if (buffer.Length == 0) return Fail(400, "Choose a photo before scanning.");

            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await service.ScanAsync(
                userId,
                request.ContentType?.Split(';')[0].Trim().ToLowerInvariant(),
                request.Headers["X-RBOS-Detected-Barcode"].ToString(),
                request.Headers["X-RBOS-Detected-Symbology"].ToString(),
                buffer.ToArray(), ct);
            return Reply(result);
        }).RequireAuthorization();
    }
}
