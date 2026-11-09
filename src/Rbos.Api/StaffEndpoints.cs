using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Rbos.Api.Persistence;
using Rbos.Domain;
using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record StaffCreateRequest(string Email, string Password, string Role);
public sealed record StaffUpdateRequest(string? Role, bool? Active);

/// <summary>G1: staff accounts for the owner's business. Access to every route is gated by D2 (staff.manage).</summary>
public static class StaffEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/staff/roles", () => Results.Ok(Permissions.Roles.Where(r => r != "Owner").Select(r => new { role = r, permissions = Permissions.For(r) })));

        api.MapGet("/staff", async (ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            await using var s = await db.BeginAsync(uid, tid, ct);
            await using var c = s.Command(@"select m.user_id, u.""Email"", m.role, m.status, m.created_at
                from core.tenant_membership m join identity.""AspNetUsers"" u on u.""Id"" = m.user_id
                where m.tenant_id = @t order by m.created_at");
            c.Parameters.AddWithValue("t", tid.Value);
            var list = new List<object>();
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(new { userId = r.GetGuid(0), email = r.IsDBNull(1) ? null : r.GetString(1), role = r.GetString(2), active = r.GetString(3) == "Active", createdAt = r.GetDateTime(4), isYou = r.GetGuid(0) == uid });
            return Results.Ok(list);
        });

        api.MapPost("/staff", async (StaffCreateRequest m, ClaimsPrincipal p, TenantDb db, UserManager<IdentityUser<Guid>> um, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            if (!Permissions.IsKnownRole(m.Role) || m.Role == "Owner") return Fail(400, "Choose a valid staff role.");
            var email = (m.Email ?? "").Trim();
            if (email.Length is 0 or > 254 || !email.Contains('@')) return Fail(400, "Enter a valid email address.");
            if (await um.FindByEmailAsync(email) is not null) return Fail(409, "An account with this email already exists.");

            var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
            var created = await um.CreateAsync(user, m.Password ?? "");
            if (!created.Succeeded) return Fail(400, string.Join(" ", created.Errors.Select(e => e.Description)));

            await using var s = await db.BeginAsync(uid, tid, ct);
            await using var c = s.Command("insert into core.tenant_membership(tenant_id,user_id,role,status) values (@t,@u,@r,'Active')");
            c.Parameters.AddWithValue("t", tid.Value);
            c.Parameters.AddWithValue("u", user.Id);
            c.Parameters.AddWithValue("r", m.Role);
            await c.ExecuteNonQueryAsync(ct);
            await s.CommitAsync(ct);
            return Results.Ok(new { userId = user.Id, email, role = m.Role });
        });

        api.MapPut("/staff/{userId:guid}", async (Guid userId, StaffUpdateRequest m, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            if (userId == uid) return Fail(400, "You cannot change your own access.");
            if (m.Role is not null && (!Permissions.IsKnownRole(m.Role) || m.Role == "Owner")) return Fail(400, "Choose a valid staff role.");
            await using var s = await db.BeginAsync(uid, tid, ct);
            await using var c = s.Command(@"update core.tenant_membership set role = coalesce(@r, role),
                status = case when @a is null then status when @a then 'Active' else 'Revoked' end
                where tenant_id = @t and user_id = @u and role <> 'Owner'");
            c.Parameters.AddWithValue("r", (object?)m.Role ?? DBNull.Value);
            c.Parameters.AddWithValue("a", NpgsqlTypes.NpgsqlDbType.Boolean, (object?)m.Active ?? DBNull.Value);
            c.Parameters.AddWithValue("t", tid.Value);
            c.Parameters.AddWithValue("u", userId);
            if (await c.ExecuteNonQueryAsync(ct) == 0) return Fail(404, "Staff member not found.");
            await s.CommitAsync(ct);
            return Results.Ok(new { userId });
        });
    }

    static async Task<(Guid Uid, Guid? Tid)> Ctx(ClaimsPrincipal p, TenantDb db, CancellationToken ct)
    {
        var uid = Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return (uid, await db.FindTenantAsync(uid, ct));
    }
}
