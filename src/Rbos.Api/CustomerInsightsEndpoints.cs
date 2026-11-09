using System.Security.Claims;
using Rbos.Api.Application;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public static class CustomerInsightsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/customers/{id:guid}/history", async (Guid id, ClaimsPrincipal p, CustomerInsightsService svc, CancellationToken ct) => Reply(await svc.HistoryAsync(Uid(p), id, ct))).RequireAuthorization();
        api.MapGet("/customers/ageing", async (ClaimsPrincipal p, CustomerInsightsService svc, CancellationToken ct) => Reply(await svc.DuesAgeingAsync(Uid(p), DateOnly.FromDateTime(DateTime.UtcNow), ct))).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
