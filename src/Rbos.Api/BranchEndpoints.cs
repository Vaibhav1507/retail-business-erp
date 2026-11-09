using System.Security.Claims;
using Rbos.Api.Persistence;
using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record BranchCreateRequest(string Name, string? LocationName);
public sealed record LocationCreateRequest(Guid BranchId, string Name);

/// <summary>G2: branches and their stock locations. Sales, purchases and stock already take a locationId.</summary>
public static class BranchEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/branches", async (ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            await using var s = await db.BeginAsync(uid, tid, ct);
            await using var c = s.Command(@"select b.id, b.name, b.is_primary, l.id, l.name
                from core.branch b left join core.stock_location l on l.tenant_id = b.tenant_id and l.branch_id = b.id
                order by b.is_primary desc, b.name, l.name");
            var map = new Dictionary<Guid, (string Name, bool Primary, List<object> Locs)>();
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var id = r.GetGuid(0);
                if (!map.TryGetValue(id, out var b)) map[id] = b = (r.GetString(1), r.GetBoolean(2), []);
                if (!r.IsDBNull(3)) b.Locs.Add(new { id = r.GetGuid(3), name = r.GetString(4) });
            }
            return Results.Ok(map.Select(x => new { id = x.Key, name = x.Value.Name, isPrimary = x.Value.Primary, locations = x.Value.Locs }));
        });

        api.MapPost("/branches", async (BranchCreateRequest m, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            var name = (m.Name ?? "").Trim();
            if (name.Length is 0 or > 120) return Fail(400, "Branch name must be 1-120 characters.");
            var loc = string.IsNullOrWhiteSpace(m.LocationName) ? name : m.LocationName.Trim();
            if (loc.Length > 120) return Fail(400, "Location name must be at most 120 characters.");
            var bid = Guid.NewGuid(); var lid = Guid.NewGuid();
            await using var s = await db.BeginAsync(uid, tid, ct);
            await using (var c = s.Command("insert into core.branch(tenant_id,id,name,is_primary) values (@t,@b,@n,false)"))
            { c.Parameters.AddWithValue("t", tid.Value); c.Parameters.AddWithValue("b", bid); c.Parameters.AddWithValue("n", name); await c.ExecuteNonQueryAsync(ct); }
            await using (var c = s.Command("insert into core.stock_location(tenant_id,id,branch_id,name) values (@t,@l,@b,@n)"))
            { c.Parameters.AddWithValue("t", tid.Value); c.Parameters.AddWithValue("l", lid); c.Parameters.AddWithValue("b", bid); c.Parameters.AddWithValue("n", loc); await c.ExecuteNonQueryAsync(ct); }
            await s.CommitAsync(ct);
            return Results.Ok(new { id = bid, locationId = lid });
        });

        api.MapPost("/locations", async (LocationCreateRequest m, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            var name = (m.Name ?? "").Trim();
            if (name.Length is 0 or > 120) return Fail(400, "Location name must be 1-120 characters.");
            await using var s = await db.BeginAsync(uid, tid, ct);
            await using var ck = s.Command("select 1 from core.branch where id = @b");
            ck.Parameters.AddWithValue("b", m.BranchId);
            if (await ck.ExecuteScalarAsync(ct) is null) return Fail(404, "Branch not found.");
            var lid = Guid.NewGuid();
            await using var c = s.Command("insert into core.stock_location(tenant_id,id,branch_id,name) values (@t,@l,@b,@n)");
            c.Parameters.AddWithValue("t", tid.Value); c.Parameters.AddWithValue("l", lid); c.Parameters.AddWithValue("b", m.BranchId); c.Parameters.AddWithValue("n", name);
            await c.ExecuteNonQueryAsync(ct);
            await s.CommitAsync(ct);
            return Results.Ok(new { id = lid });
        });
    }

    static async Task<(Guid Uid, Guid? Tid)> Ctx(ClaimsPrincipal p, TenantDb db, CancellationToken ct)
    {
        var uid = Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return (uid, await db.FindTenantAsync(uid, ct));
    }
}
