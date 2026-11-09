using System.Security.Claims;
using Rbos.Api.Application;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record BarcodeRequest(string? Code, string? Symbology, int? PackQuantity);
public sealed record SerialTrackingRequest(bool Enabled);

public static class BarcodeEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/products/by-barcode", async (string? code, ClaimsPrincipal p, BarcodeService svc, CancellationToken ct)
            => Reply(await svc.LookupAsync(Uid(p), code, ct))).RequireAuthorization();

        api.MapGet("/products/{id:guid}/barcodes", async (Guid id, ClaimsPrincipal p, BarcodeService svc, CancellationToken ct)
            => Reply(await svc.ListAsync(Uid(p), id, ct))).RequireAuthorization();

        api.MapPost("/products/{id:guid}/barcodes", async (Guid id, BarcodeRequest m, ClaimsPrincipal p, BarcodeService svc, CancellationToken ct)
            => Reply(await svc.AddAsync(Uid(p), id, new BarcodeCommand(m.Code, m.Symbology, m.PackQuantity), ct))).RequireAuthorization();

        api.MapDelete("/products/{id:guid}/barcodes/{barcodeId:guid}", async (Guid id, Guid barcodeId, ClaimsPrincipal p, BarcodeService svc, CancellationToken ct)
            => Reply(await svc.RemoveAsync(Uid(p), id, barcodeId, ct))).RequireAuthorization();

        api.MapPut("/products/{id:guid}/serial-tracking", async (Guid id, SerialTrackingRequest m, ClaimsPrincipal p, BarcodeService svc, CancellationToken ct)
            => Reply(await svc.SetSerialTrackingAsync(Uid(p), id, m.Enabled, ct))).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
