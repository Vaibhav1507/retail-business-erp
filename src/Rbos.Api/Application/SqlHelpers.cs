using System.Security.Cryptography;
using System.Text;
using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

/// <summary>Small helpers shared by the SQL-based application services.</summary>
internal static class SqlHelpers
{
    public static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public static string Hash(string v) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(v)));

    public static async Task Exec(TenantSession s, CancellationToken ct, string sql, params (string, object?)[] ps)
    {
        await using var c = s.Command(sql);
        foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
        await c.ExecuteNonQueryAsync(ct);
    }

    public static async Task<T?> Scalar<T>(TenantSession s, string sql, CancellationToken ct, params (string, object?)[] ps)
    {
        await using var c = s.Command(sql);
        foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
        var r = await c.ExecuteScalarAsync(ct);
        return r is null or DBNull ? default : (T)r;
    }

    /// <summary>Returns the earlier result for an idempotency key (replay), a 409 if the key was reused with different content, or null if the key is new. <paramref name="table"/> must be a trusted constant.</summary>
    public static async Task<ServiceResult<object>?> Prior(TenantSession s, string table, string key, string hash, CancellationToken ct)
    {
        await using var q = s.Command($"select id, request_hash from {table} where idempotency_key=@k");
        q.Parameters.AddWithValue("k", key);
        await using var r = await q.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return r.GetString(1) == hash ? ServiceResult<object>.Ok(new { id = r.GetGuid(0), replayed = true }) : ServiceResult<object>.Fail("Idempotency-Key reused with a different request.", 409);
    }
}
