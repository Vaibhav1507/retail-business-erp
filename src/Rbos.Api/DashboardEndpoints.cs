using System.Security.Claims;
using Rbos.Api.Application;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public static class DashboardEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", async (ClaimsPrincipal p, DashboardService svc, CancellationToken ct) => Reply(await svc.SummaryAsync(Uid(p), DateOnly.FromDateTime(DateTime.UtcNow), ct))).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

