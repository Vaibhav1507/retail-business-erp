using System.Security.Claims;
using Rbos.Api.Persistence;
using Rbos.Domain;
using static Rbos.Api.ApiResults;

namespace Rbos.Api;

/// <summary>D2: authorises each /api call by the operation it performs (Permissions.Required), using the caller's membership role.</summary>
public static class PermissionFilter
{
    public static void Apply(RouteGroupBuilder api) => api.AddEndpointFilter(async (ctx, next) =>
    {
        var http = ctx.HttpContext;
        var needed = Permissions.Required(http.Request.Method, http.Request.Path.Value ?? "");
        if (needed is null) return await next(ctx);

        var idClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var uid)) return await next(ctx);

        var db = http.RequestServices.GetRequiredService<TenantDb>();
        var role = await db.FindRoleAsync(uid, http.RequestAborted);
        if (role is null) return await next(ctx);
        return Permissions.Has(role, needed) ? await next(ctx) : Fail(403, "You do not have permission to do this.");
    });
}
