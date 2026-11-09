using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

/// <summary>D6: read-only, searchable audit history.</summary>
public sealed class AuditHistoryService(TenantDb db)
{
    public async Task<ServiceResult<object>> SearchAsync(Guid uid, string? q, string? action, DateOnly? from, DateOnly? to, int limit, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        limit = Math.Clamp(limit, 1, 200);
        await using var s = await db.BeginAsync(uid, tid, ct);
        await using var c = s.Command(@"select a.occurred_at, a.action, a.entity_type, a.entity_id, a.summary, a.correlation_id, a.actor_user_id::text
            from audit.audit_event a
            where (@q::text is null or a.summary ilike '%' || @q || '%' or a.action ilike '%' || @q || '%' or a.entity_type ilike '%' || @q || '%')
              and (@a::text is null or a.action = @a)
              and (@f::date is null or a.occurred_at >= @f::date)
              and (@t::date is null or a.occurred_at < @t::date + 1)
            order by a.occurred_at desc limit @n");
        c.Parameters.AddWithValue("q", (object?)(string.IsNullOrWhiteSpace(q) ? null : q.Trim()) ?? DBNull.Value);
        c.Parameters.AddWithValue("a", (object?)(string.IsNullOrWhiteSpace(action) ? null : action) ?? DBNull.Value);
        c.Parameters.AddWithValue("f", NpgsqlTypes.NpgsqlDbType.Date, (object?)from ?? DBNull.Value);
        c.Parameters.AddWithValue("t", NpgsqlTypes.NpgsqlDbType.Date, (object?)to ?? DBNull.Value);
        c.Parameters.AddWithValue("n", limit);
        var rows = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            rows.Add(new { at = r.GetDateTime(0), action = r.GetString(1), entityType = r.GetString(2), entityId = r.GetGuid(3), summary = r.GetString(4), correlationId = r.IsDBNull(5) ? null : r.GetString(5), actor = r.GetString(6) });
        return ServiceResult<object>.Ok(rows);
    }
}
