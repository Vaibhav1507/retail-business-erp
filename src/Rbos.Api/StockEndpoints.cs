using System.Security.Claims;
using Rbos.Api.Application;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record UnitConversionRequest(Guid ProductId, string Unit, decimal Factor);
public sealed record ReorderLevelRequest(Guid ProductId, decimal Level);
public sealed record StockCountRequest(Guid LocationId, DateOnly Date, List<StockCountLine> Lines);

public static class StockEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/unit-conversions", async (Guid? productId, ClaimsPrincipal p, StockInsightsService s, CancellationToken ct) => Reply(await s.UnitConversionsAsync(Uid(p), productId, ct))).RequireAuthorization();
        api.MapPut("/unit-conversions", async (UnitConversionRequest m, ClaimsPrincipal p, StockInsightsService s, CancellationToken ct) => Reply(await s.SetUnitConversionAsync(Uid(p), new(m.ProductId, m.Unit, m.Factor), ct))).RequireAuthorization();
        api.MapGet("/stock-count/sheet", async (Guid locationId, ClaimsPrincipal p, StockInsightsService s, CancellationToken ct) => Reply(await s.CountSheetAsync(Uid(p), locationId, ct))).RequireAuthorization();
        api.MapPost("/stock-count", async (StockCountRequest m, HttpRequest r, ClaimsPrincipal p, StockInsightsService s, CancellationToken ct) => Reply(await s.ApplyCountAsync(Uid(p), r.Headers["Idempotency-Key"].FirstOrDefault(), new(m.LocationId, m.Date, m.Lines), ct))).RequireAuthorization();
        api.MapGet("/stock-adjustments/history", async (Guid? productId, string? reason, DateOnly? from, DateOnly? to, ClaimsPrincipal p, StockInsightsService s, CancellationToken ct) => Reply(await s.AdjustmentHistoryAsync(Uid(p), productId, reason, from, to, ct))).RequireAuthorization();
        api.MapPut("/reorder-levels", async (ReorderLevelRequest m, ClaimsPrincipal p, StockInsightsService s, CancellationToken ct) => Reply(await s.SetReorderLevelAsync(Uid(p), new(m.ProductId, m.Level), ct))).RequireAuthorization();
        api.MapGet("/stock/low", async (ClaimsPrincipal p, StockInsightsService s, CancellationToken ct) => Reply(await s.LowStockAsync(Uid(p), ct))).RequireAuthorization();
        api.MapGet("/stock/dead", async (int? days, ClaimsPrincipal p, StockInsightsService s, CancellationToken ct) => Reply(await s.DeadStockAsync(Uid(p), days ?? 90, DateOnly.FromDateTime(DateTime.UtcNow), ct))).RequireAuthorization();
        api.MapGet("/stock/ageing", async (ClaimsPrincipal p, StockInsightsService s, CancellationToken ct) => Reply(await s.StockAgeingAsync(Uid(p), DateOnly.FromDateTime(DateTime.UtcNow), ct))).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
