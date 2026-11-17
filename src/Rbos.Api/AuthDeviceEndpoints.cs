using System.Security.Claims;

namespace Rbos.Api;

public static class AuthDeviceEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/auth/devices", async (ClaimsPrincipal principal, AuthSessionService sessions, HttpContext context, CancellationToken ct) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Results.Ok(await sessions.GetDevicesAsync(userId, context.User, ct));
        }).RequireAuthorization();

        api.MapPost("/auth/devices/{id:guid}/revoke", async (Guid id, ClaimsPrincipal principal, AuthSessionService sessions, CancellationToken ct) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var revoked = await sessions.RevokeDeviceAsync(userId, id, ct);
            return revoked ? Results.Ok(new { revoked = true }) : Results.NotFound();
        }).RequireAuthorization();
    }
}
