using System.Security.Claims;
using Rbos.Api.Application;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record BrandRequest(string? Name, string? Website, List<string>? Aliases, bool? IsActive);

public static class BrandEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/brands", async (bool? includeInactive, ClaimsPrincipal p, BrandService svc, CancellationToken ct)
            => Reply(await svc.ListAsync(Uid(p), includeInactive ?? false, ct))).RequireAuthorization();

        api.MapPost("/brands", async (BrandRequest m, ClaimsPrincipal p, BrandService svc, CancellationToken ct)
            => Reply(await svc.CreateAsync(Uid(p), new BrandCommand(m.Name, m.Website, m.Aliases, m.IsActive ?? true), ct))).RequireAuthorization();

        api.MapPut("/brands/{id:guid}", async (Guid id, BrandRequest m, ClaimsPrincipal p, BrandService svc, CancellationToken ct)
            => Reply(await svc.UpdateAsync(Uid(p), id, new BrandCommand(m.Name, m.Website, m.Aliases, m.IsActive ?? true), ct))).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
