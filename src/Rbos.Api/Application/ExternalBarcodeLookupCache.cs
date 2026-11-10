using System.Text.Json;
using Npgsql;
using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

public class ExternalBarcodeLookupCache(TenantDb db)
{
    static TimeSpan TtlFor(ExternalLookupStatus s) => s switch
    {
        ExternalLookupStatus.Found => TimeSpan.FromHours(24),
        ExternalLookupStatus.NotFound => TimeSpan.FromHours(12),
        ExternalLookupStatus.Timeout => TimeSpan.FromMinutes(15),
        ExternalLookupStatus.RateLimited => TimeSpan.FromMinutes(30),
        ExternalLookupStatus.Unavailable => TimeSpan.FromMinutes(20),
        _ => TimeSpan.FromMinutes(20),
    };

    public virtual async Task<ProviderLookupResult?> GetAsync(Guid uid, string barcode, string provider, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return null;
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await using var c = s.Command(@"select status,payload_json,attribution,looked_up_at,expires_at from catalog.external_barcode_lookup_cache
            where barcode=@b and provider=@p and expires_at > now()");
        c.Parameters.AddWithValue("b", barcode);
        c.Parameters.AddWithValue("p", provider);
        await using var r = await c.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        var status = Enum.Parse<ExternalLookupStatus>(r.GetString(0));
        ExternalProductInfo? product = null;
        if (!r.IsDBNull(1))
        {
            var payload = r.GetString(1);
            var cached = JsonSerializer.Deserialize<ExternalProductInfo>(payload);
            if (cached is not null) product = cached;
        }
        var lookedAt = r.GetFieldValue<DateTimeOffset>(3);
        return new ProviderLookupResult(provider, status, product, LookedUpAtUtc: lookedAt);
    }

    public virtual async Task UpsertAsync(Guid uid, string barcode, ProviderLookupResult result, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return;
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        var lookedAt = result.LookedUpAtUtc ?? DateTimeOffset.UtcNow;
        var expires = lookedAt.Add(TtlFor(result.Status));
        var payload = result.Product is null ? null : JsonSerializer.Serialize(result.Product);
        await using var c = s.Command(@"insert into catalog.external_barcode_lookup_cache
            (tenant_id, barcode, provider, status, payload_json, attribution, looked_up_at, expires_at)
            values (@t,@b,@p,@s,@j,@a,@l,@e)
            on conflict (tenant_id, barcode, provider)
            do update set status=excluded.status, payload_json=excluded.payload_json, attribution=excluded.attribution,
            looked_up_at=excluded.looked_up_at, expires_at=excluded.expires_at");
        c.Parameters.AddWithValue("t", tid.Value);
        c.Parameters.AddWithValue("b", barcode);
        c.Parameters.AddWithValue("p", result.Provider);
        c.Parameters.AddWithValue("s", result.Status.ToString());
        c.Parameters.AddWithValue("j", (object?)payload ?? DBNull.Value);
        c.Parameters.AddWithValue("a", (object?)result.Product?.Attribution ?? DBNull.Value);
        c.Parameters.AddWithValue("l", lookedAt);
        c.Parameters.AddWithValue("e", expires);
        await c.ExecuteNonQueryAsync(ct);
        await s.CommitAsync(ct);
    }
}
