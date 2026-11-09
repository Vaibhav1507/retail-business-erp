using System.Security.Claims;
using Npgsql;
using Rbos.Api.Persistence;
using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record MoveLineRequest(Guid ProductId, decimal Quantity);
public sealed record MoveRequest(DateOnly Date, Guid FromLocationId, Guid ToLocationId, string? Note, List<MoveLineRequest> Lines);

/// <summary>B7: stock moves between locations. Value moves at the product's current WAC; total quantity and ledger are untouched.</summary>
public static class LocationMoveEndpoints
{
    const string MovementSql = "insert into inventory.stock_movement(tenant_id,id,product_id,location_id,quantity_delta,unit_cost,value_delta,effective_date,source_type,source_id) values (@t,@m,@p,@l,@q,@c,@v,@d,'StockTransfer',@s)";

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/stock-transfers", async (ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            await using var s = await db.BeginAsync(uid, tid, ct);
            await using var c = s.Command(@"select t.id, t.move_date, f.name, d.name, t.note,
                    (select string_agg(pr.name || ' x ' || l.quantity::text, ', ' order by pr.name) from inventory.location_move_line l join catalog.product pr on pr.id = l.product_id and pr.tenant_id = l.tenant_id where l.move_id = t.id)
                from inventory.location_move t join core.stock_location f on f.id = t.from_location_id join core.stock_location d on d.id = t.to_location_id
                order by t.move_date desc, t.created_at desc limit 100");
            var list = new List<object>();
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(new { id = r.GetGuid(0), date = DateOnly.FromDateTime(r.GetDateTime(1)), from = r.GetString(2), to = r.GetString(3), note = r.IsDBNull(4) ? null : r.GetString(4), items = r.IsDBNull(5) ? "" : r.GetString(5) });
            return Results.Ok(list);
        });

        api.MapPost("/stock-transfers", async (MoveRequest m, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            if (m.FromLocationId == m.ToLocationId) return Fail(400, "Choose two different locations.");
            if (m.Lines is not { Count: > 0 and <= 100 }) return Fail(400, "Add between 1 and 100 items.");
            if (m.Lines.Any(l => l.Quantity <= 0 || decimal.Round(l.Quantity, 3) != l.Quantity)) return Fail(400, "Quantities must be positive (up to 3 decimals).");
            if (m.Lines.Select(l => l.ProductId).Distinct().Count() != m.Lines.Count) return Fail(400, "Each product may appear once.");
            if ((m.Note?.Length ?? 0) > 300) return Fail(400, "Note is too long.");

            try
            {
                await using var s = await db.BeginAsync(uid, tid, ct);
                var t = tid.Value;
                foreach (var loc in new[] { m.FromLocationId, m.ToLocationId })
                {
                    await using var ck = s.Command("select 1 from core.stock_location where id = @i");
                    ck.Parameters.AddWithValue("i", loc);
                    if (await ck.ExecuteScalarAsync(ct) is null) return Fail(404, "Unknown location.");
                }
                var id = Guid.NewGuid();
                await Exec(s, ct, "insert into inventory.location_move(tenant_id,id,move_date,from_location_id,to_location_id,note,created_by) values (@t,@i,@d,@f,@o,@n,@u)",
                    ("t", t), ("i", id), ("d", m.Date), ("f", m.FromLocationId), ("o", m.ToLocationId), ("n", string.IsNullOrWhiteSpace(m.Note) ? DBNull.Value : m.Note.Trim()), ("u", uid));

                foreach (var l in m.Lines)
                {
                    await using var q = s.Command("select quantity from inventory.stock_balance where product_id = @p and location_id = @l for update");
                    q.Parameters.AddWithValue("p", l.ProductId); q.Parameters.AddWithValue("l", m.FromLocationId);
                    var onHand = await q.ExecuteScalarAsync(ct) as decimal? ?? 0m;
                    if (l.Quantity > onHand) return Fail(409, "Not enough stock at the source location.");
                    await using var w = s.Command("select wac from inventory.item_cost where product_id = @p");
                    w.Parameters.AddWithValue("p", l.ProductId);
                    var wac = await w.ExecuteScalarAsync(ct) as decimal? ?? 0m;
                    var value = decimal.Round(l.Quantity * wac, 2, MidpointRounding.AwayFromZero);
                    if (wac <= 0 || value <= 0) return Fail(409, "Product has no cost yet; transfer is not possible.");

                    await Exec(s, ct, "insert into inventory.location_move_line(tenant_id,move_id,product_id,quantity) values (@t,@i,@p,@q)", ("t", t), ("i", id), ("p", l.ProductId), ("q", l.Quantity));
                    await Exec(s, ct, MovementSql, ("t", t), ("m", Guid.NewGuid()), ("p", l.ProductId), ("l", m.FromLocationId), ("q", -l.Quantity), ("c", wac), ("v", -value), ("d", m.Date), ("s", id));
                    await Exec(s, ct, MovementSql, ("t", t), ("m", Guid.NewGuid()), ("p", l.ProductId), ("l", m.ToLocationId), ("q", l.Quantity), ("c", wac), ("v", value), ("d", m.Date), ("s", id));
                    await Exec(s, ct, "update inventory.stock_balance set quantity = quantity - @q where product_id = @p and location_id = @l", ("q", l.Quantity), ("p", l.ProductId), ("l", m.FromLocationId));
                    await Exec(s, ct, "insert into inventory.stock_balance(tenant_id,product_id,location_id,quantity) values (@t,@p,@l,@q) on conflict (tenant_id,product_id,location_id) do update set quantity = inventory.stock_balance.quantity + @q",
                        ("t", t), ("p", l.ProductId), ("l", m.ToLocationId), ("q", l.Quantity));
                }
                await s.CommitAsync(ct);
                return Results.Ok(new { id });
            }
            catch (PostgresException e) when (e.SqlState == "RB001") { return Fail(409, e.MessageText); }
            catch (PostgresException e) when (e.SqlState == "23503") { return Fail(404, "Unknown product or location."); }
        });
    }

    static async Task Exec(TenantSession s, CancellationToken ct, string sql, params (string, object)[] ps)
    {
        await using var c = s.Command(sql);
        foreach (var (k, v) in ps) c.Parameters.AddWithValue(k, v);
        await c.ExecuteNonQueryAsync(ct);
    }

    static async Task<(Guid Uid, Guid? Tid)> Ctx(ClaimsPrincipal p, TenantDb db, CancellationToken ct)
    {
        var uid = Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return (uid, await db.FindTenantAsync(uid, ct));
    }
}
