using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Rbos.Api.Persistence;
using Rbos.Domain;

namespace Rbos.Api.Application;

public sealed record ProductCommand(string? Name, string? CategoryName, bool IsGeneric, string? BaseUnit, string? Brand, string? Model, Dictionary<string, string>? Attributes, bool IsActive = true)
{
    public ProductInput ToInput() => new(Name, CategoryName, IsGeneric, BaseUnit, Brand, Model, Attributes);
}

public sealed record ImportRowInput(string? Name, string? CategoryName, bool IsGeneric, string? BaseUnit, string? Brand, string? Model, Dictionary<string, string>? Attributes)
{
    public ProductInput ToInput() => new(Name, CategoryName, IsGeneric, BaseUnit, Brand, Model, Attributes);
}

public sealed record ImportRowResult(int Row, string Status, string? Message);
public sealed record ImportPreview(IReadOnlyList<ImportRowResult> Rows, int Ok, int Duplicate, int Invalid, IReadOnlyList<string> NewCategories);
public sealed record ImportCommitResult(int Created, int Skipped, bool Replayed);
public sealed record ServiceResult<T>(T? Value, string? Error, int Status)
{
    public static ServiceResult<T> Ok(T v) => new(v, null, 200);
    public static ServiceResult<T> Fail(string error, int status = 400) => new(default, error, status);
}

public sealed class CatalogueService(TenantDb db)
{
    const int MaxImportRows = ProductRules.MaxImportRows;

    public async Task<ServiceResult<object>> SearchAsync(Guid uid, string? q, Guid? categoryId, bool includeInactive, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await using var c = s.Command(@"select p.id,p.name,p.category_id,c.name,p.is_generic,p.internal_sku,p.base_unit,p.brand,p.model,p.attributes::text,p.is_active
            from catalog.product p join catalog.category c on c.tenant_id=p.tenant_id and c.id=p.category_id
            where (@act or p.is_active)
              and (@cat::uuid is null or p.category_id=@cat)
              and (@q::text is null or p.name ilike @q or p.brand ilike @q or p.model ilike @q or p.internal_sku ilike @q or p.attributes::text ilike @q)
            order by p.name limit 300");
        c.Parameters.AddWithValue("act", includeInactive);
        c.Parameters.AddWithValue("cat", (object?)categoryId ?? DBNull.Value);
        c.Parameters.AddWithValue("q", string.IsNullOrWhiteSpace(q) ? DBNull.Value : "%" + Like(q.Trim()) + "%");
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new
            {
                id = r.GetGuid(0), name = r.GetString(1), categoryId = r.GetGuid(2), category = r.GetString(3), isGeneric = r.GetBoolean(4), sku = r.GetString(5), unit = r.GetString(6),
                brand = r.IsDBNull(7) ? null : r.GetString(7), model = r.IsDBNull(8) ? null : r.GetString(8),
                attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(9)) ?? new(), isActive = r.GetBoolean(10),
            });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> CategoriesAsync(Guid uid, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await using var c = s.Command("select c.id,c.name,(select count(*) from catalog.product p where p.tenant_id=c.tenant_id and p.category_id=c.id and p.is_active) from catalog.category c order by c.name");
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new { id = r.GetGuid(0), name = r.GetString(1), productCount = r.GetInt64(2) });
        return ServiceResult<object>.Ok(list);
    }

    public async Task<ServiceResult<object>> RenameCategoryAsync(Guid uid, Guid id, string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80) return ServiceResult<object>.Fail("Category name is required (max 80 characters).");
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        try
        {
            await using var c = s.Command("update catalog.category set name=@n where id=@i");
            c.Parameters.AddWithValue("n", name.Trim()); c.Parameters.AddWithValue("i", id);
            if (await c.ExecuteNonQueryAsync(ct) == 0) return ServiceResult<object>.Fail("Category not found.", 404);
        }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("A category with that name already exists.", 409); }
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id, name = name.Trim() });
    }

    public async Task<ServiceResult<object>> CreateAsync(Guid uid, ProductCommand m, CancellationToken ct)
    {
        var err = ProductRules.Validate(m.ToInput());
        if (err is not null) return ServiceResult<object>.Fail(err);
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        var cat = await EnsureCategoryAsync(s, tid.Value, m.CategoryName!, ct);
        var dup = await FindDuplicateAsync(s, m.Name, m.Brand, m.Model, null, ct);
        if (dup is not null) return ServiceResult<object>.Fail($"A product with the same name, brand and model already exists ({dup.Value.sku}).", 409);
        var first = await NextSkuAsync(s, tid.Value, 1, ct);
        var id = Guid.NewGuid();
        var sku = ProductRules.FormatSku(first);
        try { await InsertAsync(s, tid.Value, id, cat, sku, m.ToInput(), ct); }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("A product with the same name, brand and model already exists.", 409); }
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id, sku });
    }

    public async Task<ServiceResult<object>> UpdateAsync(Guid uid, Guid id, ProductCommand m, CancellationToken ct)
    {
        var err = ProductRules.Validate(m.ToInput());
        if (err is not null) return ServiceResult<object>.Fail(err);
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        var cat = await EnsureCategoryAsync(s, tid.Value, m.CategoryName!, ct);
        if (m.IsActive)
        {
            var dup = await FindDuplicateAsync(s, m.Name, m.Brand, m.Model, id, ct);
            if (dup is not null) return ServiceResult<object>.Fail($"A product with the same name, brand and model already exists ({dup.Value.sku}).", 409);
        }
        try
        {
            await using var c = s.Command("update catalog.product set category_id=@c,name=@n,is_generic=@g,brand=@b,model=@m,attributes=@a::jsonb,is_active=@x where id=@i");
            c.Parameters.AddWithValue("c", cat); c.Parameters.AddWithValue("n", m.Name!.Trim()); c.Parameters.AddWithValue("g", m.IsGeneric);
            c.Parameters.AddWithValue("b", (object?)NullIfBlank(m.Brand) ?? DBNull.Value); c.Parameters.AddWithValue("m", (object?)NullIfBlank(m.Model) ?? DBNull.Value);
            c.Parameters.AddWithValue("a", JsonSerializer.Serialize(ProductRules.CleanAttributes(m.Attributes))); c.Parameters.AddWithValue("x", m.IsActive); c.Parameters.AddWithValue("i", id);
            if (await c.ExecuteNonQueryAsync(ct) == 0) return ServiceResult<object>.Fail("Product not found.", 404);
        }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("A product with the same name, brand and model already exists.", 409); }
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id });
    }

    public async Task<ServiceResult<ImportPreview>> PreviewImportAsync(Guid uid, List<ImportRowInput>? rows, CancellationToken ct)
    {
        if (rows is null || rows.Count == 0) return ServiceResult<ImportPreview>.Fail("No rows to import.");
        if (rows.Count > MaxImportRows) return ServiceResult<ImportPreview>.Fail($"At most {MaxImportRows} rows per import.");
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<ImportPreview>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        var (results, _) = await ClassifyAsync(s, rows, ct);
        var existing = await ExistingCategoriesAsync(s, ct);
        var newCats = rows.Where((_, i) => results[i].Status == "ok").Select(r => r.CategoryName!.Trim())
            .Where(n => !existing.Contains(n.ToLowerInvariant())).GroupBy(n => n.ToLowerInvariant()).Select(g => g.First()).ToList();
        return ServiceResult<ImportPreview>.Ok(new ImportPreview(results, results.Count(r => r.Status == "ok"), results.Count(r => r.Status.StartsWith("duplicate")), results.Count(r => r.Status == "invalid"), newCats));
    }

    public async Task<ServiceResult<ImportCommitResult>> CommitImportAsync(Guid uid, string? key, List<ImportRowInput>? rows, CancellationToken ct)
    {
        if (key is null || key.Length is < 8 or > 100) return ServiceResult<ImportCommitResult>.Fail("Idempotency-Key header (8-100 chars) required.");
        if (rows is null || rows.Count == 0) return ServiceResult<ImportCommitResult>.Fail("No rows to import.");
        if (rows.Count > MaxImportRows) return ServiceResult<ImportCommitResult>.Fail($"At most {MaxImportRows} rows per import.");
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<ImportCommitResult>.Fail("No business set up.", 409);
        var hash = SqlHelpers.Hash(JsonSerializer.Serialize(rows));

        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await using (var q = s.Command("select request_hash,created_count,skipped_count from catalog.import_batch where idempotency_key=@k"))
        {
            q.Parameters.AddWithValue("k", key);
            await using var r = await q.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
                return r.GetString(0) == hash
                    ? ServiceResult<ImportCommitResult>.Ok(new ImportCommitResult(r.GetInt32(1), r.GetInt32(2), true))
                    : ServiceResult<ImportCommitResult>.Fail("Idempotency key was already used with a different import.", 409);
        }

        var (results, _) = await ClassifyAsync(s, rows, ct);
        var good = rows.Where((_, i) => results[i].Status == "ok").ToList();
        var first = good.Count > 0 ? await NextSkuAsync(s, tid.Value, good.Count, ct) : 0;
        var n = 0;
        try
        {
            foreach (var row in good)
            {
                var cat = await EnsureCategoryAsync(s, tid.Value, row.CategoryName!, ct);
                await InsertAsync(s, tid.Value, Guid.NewGuid(), cat, ProductRules.FormatSku(first + n), row.ToInput(), ct);
                n++;
            }
            await using var b = s.Command("insert into catalog.import_batch(tenant_id,id,idempotency_key,request_hash,created_by,total_rows,created_count,skipped_count) values (@t,@i,@k,@h,@u,@r,@c,@s)");
            b.Parameters.AddWithValue("t", tid.Value); b.Parameters.AddWithValue("i", Guid.NewGuid()); b.Parameters.AddWithValue("k", key); b.Parameters.AddWithValue("h", hash);
            b.Parameters.AddWithValue("u", uid); b.Parameters.AddWithValue("r", rows.Count); b.Parameters.AddWithValue("c", good.Count); b.Parameters.AddWithValue("s", rows.Count - good.Count);
            await b.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<ImportCommitResult>.Fail("Import conflicted with another change. Preview again and retry.", 409); }
        await s.CommitAsync(ct);
        return ServiceResult<ImportCommitResult>.Ok(new ImportCommitResult(good.Count, rows.Count - good.Count, false));
    }

    static async Task<(List<ImportRowResult>, HashSet<string>)> ClassifyAsync(TenantSession s, List<ImportRowInput> rows, CancellationToken ct)
    {
        var existing = new HashSet<string>();
        await using (var c = s.Command("select dup_key from catalog.product where is_active"))
        {
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) existing.Add(r.GetString(0));
        }
        var seen = new HashSet<string>();
        var results = new List<ImportRowResult>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var err = ProductRules.Validate(row.ToInput());
            if (err is not null) { results.Add(new(i + 1, "invalid", err)); continue; }
            var key = ProductRules.DuplicateKey(row.Name, row.Brand, row.Model);
            if (existing.Contains(key)) results.Add(new(i + 1, "duplicate", "Already in your catalogue."));
            else if (!seen.Add(key)) results.Add(new(i + 1, "duplicate_in_file", "Repeated earlier in this file."));
            else results.Add(new(i + 1, "ok", null));
        }
        return (results, existing);
    }

    static async Task<HashSet<string>> ExistingCategoriesAsync(TenantSession s, CancellationToken ct)
    {
        var set = new HashSet<string>();
        await using var c = s.Command("select lower(btrim(name)) from catalog.category");
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) set.Add(r.GetString(0));
        return set;
    }

    static async Task<Guid> EnsureCategoryAsync(TenantSession s, Guid tid, string name, CancellationToken ct)
    {
        await using (var ins = s.Command("insert into catalog.category(tenant_id,id,name) values (@t,@i,@n) on conflict do nothing"))
        {
            ins.Parameters.AddWithValue("t", tid); ins.Parameters.AddWithValue("i", Guid.NewGuid()); ins.Parameters.AddWithValue("n", name.Trim());
            await ins.ExecuteNonQueryAsync(ct);
        }
        await using var sel = s.Command("select id from catalog.category where lower(btrim(name))=lower(btrim(@n))");
        sel.Parameters.AddWithValue("n", name.Trim());
        return (Guid)(await sel.ExecuteScalarAsync(ct))!;
    }

    static async Task<(Guid id, string sku)?> FindDuplicateAsync(TenantSession s, string? name, string? brand, string? model, Guid? excludeId, CancellationToken ct)
    {
        await using var c = s.Command("select id,internal_sku from catalog.product where is_active and dup_key=catalog.norm_text(@n)||'|'||catalog.norm_text(@b)||'|'||catalog.norm_text(@m) and (@x::uuid is null or id<>@x) limit 1");
        c.Parameters.AddWithValue("n", name!.Trim()); c.Parameters.AddWithValue("b", (object?)NullIfBlank(brand) ?? DBNull.Value); c.Parameters.AddWithValue("m", (object?)NullIfBlank(model) ?? DBNull.Value);
        c.Parameters.AddWithValue("x", (object?)excludeId ?? DBNull.Value);
        await using var r = await c.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? (r.GetGuid(0), r.GetString(1)) : null;
    }

    /// <summary>Reserves <paramref name="count"/> sequential numbers; returns the first. Row lock serialises concurrent callers.</summary>
    static async Task<long> NextSkuAsync(TenantSession s, Guid tid, int count, CancellationToken ct)
    {
        await using var c = s.Command("insert into catalog.sku_counter(tenant_id,last_value) values (@t,@n) on conflict (tenant_id) do update set last_value=catalog.sku_counter.last_value+@n returning last_value");
        c.Parameters.AddWithValue("t", tid); c.Parameters.AddWithValue("n", (long)count);
        var last = (long)(await c.ExecuteScalarAsync(ct))!;
        return last - count + 1;
    }

    static async Task InsertAsync(TenantSession s, Guid tid, Guid id, Guid cat, string sku, ProductInput m, CancellationToken ct)
    {
        await using var c = s.Command("insert into catalog.product(tenant_id,id,category_id,name,is_generic,internal_sku,base_unit,brand,model,attributes) values (@t,@i,@c,@n,@g,@s,@u,@b,@m,@a::jsonb)");
        c.Parameters.AddWithValue("t", tid); c.Parameters.AddWithValue("i", id); c.Parameters.AddWithValue("c", cat);
        c.Parameters.AddWithValue("n", m.Name!.Trim()); c.Parameters.AddWithValue("g", m.IsGeneric); c.Parameters.AddWithValue("s", sku); c.Parameters.AddWithValue("u", m.BaseUnit!);
        c.Parameters.AddWithValue("b", (object?)NullIfBlank(m.Brand) ?? DBNull.Value); c.Parameters.AddWithValue("m", (object?)NullIfBlank(m.Model) ?? DBNull.Value);
        c.Parameters.AddWithValue("a", JsonSerializer.Serialize(ProductRules.CleanAttributes(m.Attributes)));
        await c.ExecuteNonQueryAsync(ct);
    }

    static string? NullIfBlank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    static string Like(string v) => v.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
