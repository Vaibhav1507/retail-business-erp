using System.Security.Claims;
using Npgsql;
using Rbos.Api.Persistence;
using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record OrderLineRequest(Guid ProductId, decimal Quantity, decimal UnitCost);
public sealed record OrderRequest(Guid SupplierId, DateOnly Date, DateOnly? ExpectedDate, string? Note, List<OrderLineRequest> Lines);
public sealed record OrderStatusRequest(string Status, Guid? ReceiptId);

/// <summary>B8: purchase orders. Non-posting; goods are received through the existing purchase flow.</summary>
public static class OrderingEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/purchase-orders", async (ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            await using var s = await db.BeginAsync(uid, tid, ct);
            await using var c = s.Command(@"select o.id, o.number, sp.name, o.order_date, o.expected_date, o.status, o.note,
                    (select string_agg(pr.name || ' x ' || l.quantity::text, ', ' order by pr.name) from procurement.purchase_order_line l join catalog.product pr on pr.id = l.product_id and pr.tenant_id = l.tenant_id where l.order_id = o.id),
                    (select coalesce(sum(l.quantity * l.unit_cost), 0) from procurement.purchase_order_line l where l.order_id = o.id)
                from procurement.purchase_order o join procurement.supplier sp on sp.id = o.supplier_id
                order by o.created_at desc limit 200");
            var list = new List<object>();
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(new
                {
                    id = r.GetGuid(0), number = r.GetInt64(1), supplier = r.GetString(2),
                    date = DateOnly.FromDateTime(r.GetDateTime(3)), expected = r.IsDBNull(4) ? (DateOnly?)null : DateOnly.FromDateTime(r.GetDateTime(4)),
                    status = r.GetString(5), note = r.IsDBNull(6) ? null : r.GetString(6), items = r.IsDBNull(7) ? "" : r.GetString(7), total = r.GetDecimal(8),
                });
            return Results.Ok(list);
        });

        api.MapPost("/purchase-orders", async (OrderRequest m, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            if (m.Lines is not { Count: > 0 and <= 100 }) return Fail(400, "Add between 1 and 100 items.");
            if (m.Lines.Any(l => l.Quantity <= 0 || decimal.Round(l.Quantity, 3) != l.Quantity || l.UnitCost < 0)) return Fail(400, "Check quantities and costs.");
            if (m.Lines.Select(l => l.ProductId).Distinct().Count() != m.Lines.Count) return Fail(400, "Each product may appear once.");
            if ((m.Note?.Length ?? 0) > 300) return Fail(400, "Note is too long.");
            try
            {
                await using var s = await db.BeginAsync(uid, tid, ct);
                var id = Guid.NewGuid();
                await Exec(s, ct, "insert into procurement.purchase_order(tenant_id,id,supplier_id,order_date,expected_date,note,created_by) values (@t,@i,@s,@d,@e,@n,@u)",
                    ("t", tid.Value), ("i", id), ("s", m.SupplierId), ("d", m.Date), ("e", m.ExpectedDate is null ? DBNull.Value : m.ExpectedDate.Value), ("n", string.IsNullOrWhiteSpace(m.Note) ? DBNull.Value : m.Note.Trim()), ("u", uid));
                foreach (var l in m.Lines)
                    await Exec(s, ct, "insert into procurement.purchase_order_line(tenant_id,order_id,product_id,quantity,unit_cost) values (@t,@i,@p,@q,@c)",
                        ("t", tid.Value), ("i", id), ("p", l.ProductId), ("q", l.Quantity), ("c", l.UnitCost));
                await s.CommitAsync(ct);
                return Results.Ok(new { id });
            }
            catch (PostgresException e) when (e.SqlState == "23503") { return Fail(404, "Unknown supplier or product."); }
        });

        api.MapPut("/purchase-orders/{id:guid}/status", async (Guid id, OrderStatusRequest m, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var (uid, tid) = await Ctx(p, db, ct);
            if (tid is null) return Fail(409, "No business set up.");
            if (m.Status is not ("Received" or "Cancelled")) return Fail(400, "Status must be Received or Cancelled.");
            try
            {
                await using var s = await db.BeginAsync(uid, tid, ct);
                await using var c = s.Command("update procurement.purchase_order set status = @st, receipt_id = @r where id = @i and status = 'Ordered'");
                c.Parameters.AddWithValue("st", m.Status); c.Parameters.AddWithValue("i", id);
                c.Parameters.AddWithValue("r", m.ReceiptId is null ? DBNull.Value : m.ReceiptId.Value);
                if (await c.ExecuteNonQueryAsync(ct) == 0) return Fail(409, "Order not found or already closed.");
                await s.CommitAsync(ct);
                return Results.Ok(new { id });
            }
            catch (PostgresException e) when (e.SqlState == "23503") { return Fail(404, "Unknown purchase receipt."); }
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
