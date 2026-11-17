using System.Security.Claims;
using Microsoft.AspNetCore.Routing;
using Rbos.Api.Persistence;
using Rbos.Domain;
using static Rbos.Api.ApiResults;

namespace Rbos.Api;

/// <summary>
/// Enforces the explicit method + route-template policy for every endpoint in the /api group.
/// An unclassified endpoint, invalid identity or missing active membership is denied.
/// </summary>
public static class PermissionFilter
{
    public static void Apply(RouteGroupBuilder api) => api.AddEndpointFilter(async (ctx, next) =>
    {
        var http = ctx.HttpContext;
        var route = http.GetEndpoint() as RouteEndpoint;
        var template = route?.RoutePattern.RawText;
        var access = ApiRouteAccess.Resolve(http.Request.Method, template);

        // Never interpret a missing route rule as "any signed-in member".
        if (access is null)
            return Fail(403, "API route access policy is not configured.");

        if (access.Kind == ApiAccessKind.Anonymous)
            return await next(ctx);

        if (http.User.Identity?.IsAuthenticated != true)
            return Fail(401, "Authentication is required.");

        var idClaim = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var uid) || uid == Guid.Empty)
            return Fail(401, "A valid authenticated user identity is required.");

        if (access.Kind == ApiAccessKind.AuthenticatedUser)
            return await next(ctx);

        var db = http.RequestServices.GetRequiredService<TenantDb>();
        var role = await db.FindRoleAsync(uid, http.RequestAborted);
        if (!Permissions.IsKnownRole(role))
            return Fail(403, "An active business membership is required.");

        if (access.Kind == ApiAccessKind.AuthenticatedMember)
            return await next(ctx);

        if (access.Kind == ApiAccessKind.Permission && access.Permission is not null
            && Permissions.Has(role, access.Permission))
            return await next(ctx);

        return Fail(403, "You do not have permission to do this.");
    });
}
