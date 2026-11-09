using System.Security.Claims;
using Rbos.Api.Application;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public static class AuditEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/audit", async (string? q, string? action, DateOnly? from, DateOnly? to, int? limit, ClaimsPrincipal p, AuditHistoryService svc, CancellationToken ct) =>
            Reply(await svc.SearchAsync(Uid(p), q, action, from, to, limit ?? 100, ct))).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
