using System.Security.Claims;
using Rbos.Api.Application;
using static Rbos.Api.ApiResults;
namespace Rbos.Api;
public static class ReminderEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/reminders", async (ClaimsPrincipal p, ReminderService svc, CancellationToken ct) => Reply(await svc.ListAsync(Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!), DateOnly.FromDateTime(DateTime.UtcNow), ct))).RequireAuthorization();
    }
}
