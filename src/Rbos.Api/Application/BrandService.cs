using Npgsql;
using Rbos.Api.Persistence;
using Rbos.Domain;

namespace Rbos.Api.Application;

public sealed record BrandCommand(string? Name, string? Website, List<string>? Aliases, bool IsActive = true);

/// <summary>Tenant-specific brand master. Seeding and backfill are idempotent and never change a product's free-text brand.</summary>
public sealed class BrandService(TenantDb db)
{
    public async Task<ServiceResult<object>> ListAsync(Guid uid, bool includeInactive, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await PrepareAsync(s, tid.Value, ct);
        await using var c = s.Command(@"select b.id,b.name,b.website,b.aliases,b.is_active,b.is_seeded,
              (select count(*) from catalog.product p where p.tenant_id=b.tenant_id and p.brand_id=b.id)
            from catalog.brand b where (@a or b.is_active) order by b.name");
        c.Parameters.AddWithValue("a", includeInactive);
        var list = new List<object>();
        await using (var r = await c.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                list.Add(new { id = r.GetGuid(0), name = r.GetString(1), website = r.IsDBNull(2) ? null : r.GetString(2), aliases = r.GetFieldValue<string[]>(3), isActive = r.GetBoolean(4), isSeeded = r.GetBoolean(5), productCount = r.GetInt64(6) });
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> CreateAsync(Guid uid, BrandCommand m, CancellationToken ct)
    {
        var err = BrandRules.Validate(m.Name, m.Website, m.Aliases);
        if (err is not null) return ServiceResult<object>.Fail(err);
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await PrepareAsync(s, tid.Value, ct);
        var existing = await FindByNameOrAliasAsync(s, m.Name!, ct);
        if (existing is not null) return ServiceResult<object>.Fail("This brand (or an alias of it) already exists.", 409);
        var id = Guid.NewGuid();
        try { await InsertAsync(s, tid.Value, id, m.Name!.Trim(), Clean(m.Website), BrandRules.CleanAliases(m.Aliases, m.Name!), false, ct); }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("This brand already exists.", 409); }
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id });
    }

    public async Task<ServiceResult<object>> UpdateAsync(Guid uid, Guid id, BrandCommand m, CancellationToken ct)
    {
        var err = BrandRules.Validate(m.Name, m.Website, m.Aliases);
        if (err is not null) return ServiceResult<object>.Fail(err);
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        var other = await FindByNameOrAliasAsync(s, m.Name!, ct);
        if (other is not null && other != id) return ServiceResult<object>.Fail("Another brand already uses this name or alias.", 409);
        try
        {
            await using var c = s.Command("update catalog.brand set name=@n,website=@w,aliases=@a,is_active=@x where id=@i");
            c.Parameters.AddWithValue("n", m.Name!.Trim()); c.Parameters.AddWithValue("w", (object?)Clean(m.Website) ?? DBNull.Value);
            c.Parameters.AddWithValue("a", BrandRules.CleanAliases(m.Aliases, m.Name!).ToArray()); c.Parameters.AddWithValue("x", m.IsActive); c.Parameters.AddWithValue("i", id);
            if (await c.ExecuteNonQueryAsync(ct) == 0) return ServiceResult<object>.Fail("Brand not found.", 404);
        }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("Another brand already uses this name.", 409); }
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id });
    }

    /// <summary>Seeds the starter list on first use and links existing free-text product brands. Safe to repeat.</summary>
    internal static async Task PrepareAsync(TenantSession s, Guid tid, CancellationToken ct)
    {
        await using (var cnt = s.Command("select count(*) from catalog.brand"))
        {
            if ((long)(await cnt.ExecuteScalarAsync(ct))! == 0)
                foreach (var b in BrandCatalogue.Seed)
                    await InsertAsync(s, tid, Guid.NewGuid(), b.Name, b.Website, b.Aliases.ToList(), true, ct, ignoreConflict: true);
        }
        await BackfillAsync(s, tid, ct);
    }

    static async Task BackfillAsync(TenantSession s, Guid tid, CancellationToken ct)
    {
        var texts = new List<string>();
        await using (var q = s.Command("select distinct brand from catalog.product where brand_id is null and brand is not null and btrim(brand) <> ''"))
        await using (var r = await q.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) texts.Add(r.GetString(0));
        foreach (var t in texts) await ResolveAsync(s, tid, t, ct);
    }

    /// <summary>Finds the brand for free text (name or alias, ignoring case/punctuation), creating it when unknown, and links matching products.</summary>
    internal static async Task<Guid?> ResolveAsync(TenantSession s, Guid tid, string? text, CancellationToken ct)
    {
        var name = text?.Trim();
        if (string.IsNullOrEmpty(name) || BrandRules.Key(name).Length == 0 || name.Length > BrandRules.MaxName) return null;
        var id = await FindByNameOrAliasAsync(s, name, ct);
        if (id is null)
        {
            id = Guid.NewGuid();
            await InsertAsync(s, tid, id.Value, name, null, [], false, ct, ignoreConflict: true);
            id = await FindByNameOrAliasAsync(s, name, ct);
        }
        await using var u = s.Command("update catalog.product set brand_id=@b where brand_id is null and catalog.norm_text(brand)=@k");
        u.Parameters.AddWithValue("b", id!.Value); u.Parameters.AddWithValue("k", BrandRules.Key(name));
        await u.ExecuteNonQueryAsync(ct);
        return id;
    }

    static async Task<Guid?> FindByNameOrAliasAsync(TenantSession s, string name, CancellationToken ct)
    {
        await using var c = s.Command("select id from catalog.brand where norm_name=@k or exists (select 1 from unnest(aliases) a where catalog.norm_text(a)=@k) order by (norm_name=@k) desc limit 1");
        c.Parameters.AddWithValue("k", BrandRules.Key(name));
        return await c.ExecuteScalarAsync(ct) as Guid?;
    }

    static async Task InsertAsync(TenantSession s, Guid tid, Guid id, string name, string? website, List<string> aliases, bool seeded, CancellationToken ct, bool ignoreConflict = false)
    {
        await using var c = s.Command("insert into catalog.brand(tenant_id,id,name,website,aliases,is_seeded) values (@t,@i,@n,@w,@a,@s)" + (ignoreConflict ? " on conflict do nothing" : ""));
        c.Parameters.AddWithValue("t", tid); c.Parameters.AddWithValue("i", id); c.Parameters.AddWithValue("n", name.Trim());
        c.Parameters.AddWithValue("w", (object?)website ?? DBNull.Value); c.Parameters.AddWithValue("a", aliases.ToArray()); c.Parameters.AddWithValue("s", seeded);
        await c.ExecuteNonQueryAsync(ct);
    }

    static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
