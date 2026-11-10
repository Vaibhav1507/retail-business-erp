using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Rbos.Api.Persistence;
using Rbos.Domain;

namespace Rbos.Api.Application;

public sealed record ProductCommand(string? Name, string? CategoryName, bool IsGeneric, string? BaseUnit, string? Brand, string? Model, Dictionary<string, string>? Attributes, bool IsActive = true, string? HsnCode = null, decimal? GstRate = null, decimal? Mrp = null, Dictionary<string, string>? Specs = null, int? SourcePage = null)
{
    public ProductInput ToInput() => new(Name, CategoryName, IsGeneric, BaseUnit, Brand, Model, Attributes);
}

public sealed record ImportRowInput(string? Name, string? CategoryName, bool IsGeneric, string? BaseUnit, string? Brand, string? Model, Dictionary<string, string>? Attributes, string? Barcode = null, decimal? Mrp = null, Dictionary<string, string>? Specs = null, int? SourcePage = null)
{
    public ProductInput ToInput() => new(Name, CategoryName, IsGeneric, BaseUnit, Brand, Model, Attributes);
}

public sealed record ImportSource(string? Brand, string? DocumentName, string? SourceUrl, string? PublishedOn, string? VersionLabel, string? ContentHash, string? DocumentKind);
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

    public async Task<ServiceResult<object>> SearchAsync(Guid uid, string? q, Guid? categoryId, bool includeInactive, CancellationToken ct, Guid? brandId = null)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await using var c = s.Command(@"select p.id,p.name,p.category_id,c.name,p.is_generic,p.internal_sku,p.base_unit,p.brand,p.model,p.attributes::text,p.is_active,p.hsn_code,p.gst_rate,p.brand_id,p.mrp,p.specs::text,
              coalesce((select json_agg(json_build_object('documentId',d.id,'documentName',d.document_name,'brand',d.brand,'versionLabel',d.version_label,'page',sr.source_page) order by d.imported_at desc, sr.source_page)::text from catalog.product_source_reference sr join catalog.source_document d on d.tenant_id=sr.tenant_id and d.id=sr.source_document_id where sr.tenant_id=p.tenant_id and sr.product_id=p.id),'[]'),
              (p.model_norm is not null and p.model_norm<>'' and exists (select 1 from catalog.product o where o.tenant_id=p.tenant_id and o.id<>p.id and o.is_active and o.model_norm=p.model_norm and catalog.norm_text(o.brand) is not distinct from catalog.norm_text(p.brand)))
            from catalog.product p join catalog.category c on c.tenant_id=p.tenant_id and c.id=p.category_id
            where (@act or p.is_active)
              and (@cat::uuid is null or p.category_id=@cat)
              and (@br::uuid is null or p.brand_id=@br)
              and (@q::text is null or p.name ilike @q or p.brand ilike @q or p.model ilike @q or p.internal_sku ilike @q or p.attributes::text ilike @q
                   or (@qn::text is not null and p.model_norm like @qn)
                   or exists (select 1 from catalog.product_barcode pb where pb.tenant_id=p.tenant_id and pb.product_id=p.id and pb.code=@qc))
            order by p.name limit 300");
        c.Parameters.AddWithValue("act", includeInactive);
        c.Parameters.AddWithValue("cat", (object?)categoryId ?? DBNull.Value);
        c.Parameters.AddWithValue("br", (object?)brandId ?? DBNull.Value);
        c.Parameters.AddWithValue("q", string.IsNullOrWhiteSpace(q) ? DBNull.Value : "%" + Like(q.Trim()) + "%");
        var qkey = IdentifierRules.ModelKey(q);
        c.Parameters.AddWithValue("qn", qkey.Length == 0 ? DBNull.Value : "%" + Like(qkey) + "%");
        c.Parameters.AddWithValue("qc", q?.Trim() ?? "");
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new
            {
                id = r.GetGuid(0), name = r.GetString(1), categoryId = r.GetGuid(2), category = r.GetString(3), isGeneric = r.GetBoolean(4), sku = r.GetString(5), unit = r.GetString(6),
                brand = r.IsDBNull(7) ? null : r.GetString(7), model = r.IsDBNull(8) ? null : r.GetString(8),
                attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(9)) ?? new Dictionary<string, string>(), isActive = r.GetBoolean(10),
                hsnCode = r.IsDBNull(11) ? null : r.GetString(11), gstRate = r.IsDBNull(12) ? (decimal?)null : r.GetDecimal(12),
                brandId = r.IsDBNull(13) ? (Guid?)null : r.GetGuid(13), mrp = r.IsDBNull(14) ? (decimal?)null : r.GetDecimal(14),
                specs = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(15)) ?? new Dictionary<string, string>(),
                sources = JsonSerializer.Deserialize<JsonElement>(r.GetString(16)),
                possibleDuplicate = !r.IsDBNull(17) && r.GetBoolean(17),
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
        var err = ProductRules.Validate(m.ToInput()) ?? ValidateTax(m) ?? ProductRules.ValidateCatalogueData(m.Mrp, m.Specs);
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
        try { await InsertAsync(s, tid.Value, id, cat, sku, m.ToInput(), ct); await SetTaxAsync(s, id, m, ct); await SetCatalogueDataAsync(s, tid.Value, id, m.Brand, m.Mrp, WithCategoryAttributes(m.CategoryName, m.Name, m.Specs), ct); }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("A product with the same name, brand and model already exists.", 409); }
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id, sku });
    }

    static string? ValidateTax(ProductCommand m) =>
        !string.IsNullOrWhiteSpace(m.HsnCode) && !Rbos.Domain.GstCalculator.IsValidHsn(m.HsnCode.Trim()) ? "HSN code must be 4, 6 or 8 digits."
        : m.GstRate is < 0 or > 100 ? "GST rate must be between 0 and 100." : null;

    public async Task<ServiceResult<object>> UpdateAsync(Guid uid, Guid id, ProductCommand m, CancellationToken ct)
    {
        var err = ProductRules.Validate(m.ToInput()) ?? ValidateTax(m) ?? ProductRules.ValidateCatalogueData(m.Mrp, m.Specs);
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
            await SetTaxAsync(s, id, m, ct);
            await SetCatalogueDataAsync(s, tid.Value, id, m.Brand, m.Mrp, WithCategoryAttributes(m.CategoryName, m.Name, m.Specs), ct);
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

    public async Task<ServiceResult<ImportCommitResult>> CommitImportAsync(Guid uid, string? key, List<ImportRowInput>? rows, CancellationToken ct, ImportSource? source = null)
    {
        if (key is null || key.Length is < 8 or > 100) return ServiceResult<ImportCommitResult>.Fail("Idempotency-Key header (8-100 chars) required.");
        if (rows is null || rows.Count == 0) return ServiceResult<ImportCommitResult>.Fail("No rows to import.");
        if (rows.Count > MaxImportRows) return ServiceResult<ImportCommitResult>.Fail($"At most {MaxImportRows} rows per import.");
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<ImportCommitResult>.Fail("No business set up.", 409);
        var srcErr = ValidateSource(source, out var pub);
        if (srcErr is not null) return ServiceResult<ImportCommitResult>.Fail(srcErr);
        var hash = SqlHelpers.Hash(JsonSerializer.Serialize(rows) + JsonSerializer.Serialize(source));

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
        Guid? docId = null;
        try
        {
            if (source is not null && good.Count > 0) docId = await InsertSourceAsync(s, tid.Value, uid, source, pub, ct);
            foreach (var row in good)
            {
                var cat = await EnsureCategoryAsync(s, tid.Value, row.CategoryName!, ct);
                var pid = Guid.NewGuid();
                await InsertAsync(s, tid.Value, pid, cat, ProductRules.FormatSku(first + n), row.ToInput(), ct);
                await SetCatalogueDataAsync(
                    s,
                    tid.Value,
                    pid,
                    row.Brand,
                    string.Equals(source?.DocumentKind, "dealer_scheme", StringComparison.OrdinalIgnoreCase) ? null : row.Mrp,
                    WithCategoryAttributes(row.CategoryName, row.Name, row.Specs),
                    ct);
                if (docId is not null) await SetSourceAsync(s, tid.Value, pid, docId.Value, row.SourcePage, ct);
                if (!string.IsNullOrWhiteSpace(row.Barcode)) await BarcodeService.LinkIfFreeAsync(s, tid.Value, pid, row.Barcode, ct);
                n++;
            }
            await using var b = s.Command("insert into catalog.import_batch(tenant_id,id,idempotency_key,request_hash,created_by,total_rows,created_count,skipped_count,source_document_id) values (@t,@i,@k,@h,@u,@r,@c,@s,@d)");
            b.Parameters.AddWithValue("t", tid.Value); b.Parameters.AddWithValue("i", Guid.NewGuid()); b.Parameters.AddWithValue("k", key); b.Parameters.AddWithValue("h", hash);
            b.Parameters.AddWithValue("u", uid); b.Parameters.AddWithValue("r", rows.Count); b.Parameters.AddWithValue("c", good.Count); b.Parameters.AddWithValue("s", rows.Count - good.Count); b.Parameters.AddWithValue("d", (object?)docId ?? DBNull.Value);
            await b.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<ImportCommitResult>.Fail("Import conflicted with another change. Preview again and retry.", 409); }
        await s.CommitAsync(ct);
        return ServiceResult<ImportCommitResult>.Ok(new ImportCommitResult(good.Count, rows.Count - good.Count, false));
    }


	static readonly string[] DocKinds = ["catalogue", "price_list", "dealer_scheme", "other"];

	static string? ValidateSource(ImportSource? s, out DateOnly? published)
	{
		published = null;
		if (s is null) return null;
		if (string.IsNullOrWhiteSpace(s.DocumentName) || s.DocumentName.Trim().Length > 260) return "Source document name is required (max 260 characters).";
		if (s.Brand is { Length: > 80 }) return "Source brand is too long.";
		if (!string.IsNullOrWhiteSpace(s.SourceUrl) && (s.SourceUrl.Length > 500 || !Uri.TryCreate(s.SourceUrl, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https"))) return "Source URL must be a valid http(s) address.";
		if (!string.IsNullOrWhiteSpace(s.PublishedOn)) { if (!DateOnly.TryParse(s.PublishedOn, System.Globalization.CultureInfo.InvariantCulture, out var d)) return "Publication date is not valid."; published = d; }
		if (s.VersionLabel is { Length: > 80 }) return "Version label is too long.";
		if (!string.IsNullOrWhiteSpace(s.DocumentKind) && !DocKinds.Contains(s.DocumentKind)) return "Document kind must be catalogue, price_list, dealer_scheme or other.";
		return null;
	}

	static async Task<Guid> InsertSourceAsync(TenantSession s, Guid tid, Guid uid, ImportSource src, DateOnly? pub, CancellationToken ct)
	{
		var id = Guid.NewGuid();
		await using var c = s.Command("insert into catalog.source_document(tenant_id,id,brand,document_name,source_url,published_on,version_label,content_hash,document_kind,imported_by) values (@t,@i,@b,@n,@u,@p,@v,@h,@k,@by)");
		c.Parameters.AddWithValue("t", tid); c.Parameters.AddWithValue("i", id);
		c.Parameters.AddWithValue("b", (object?)NullIfBlank(src.Brand) ?? DBNull.Value);
		c.Parameters.AddWithValue("n", src.DocumentName!.Trim());
		c.Parameters.AddWithValue("u", (object?)NullIfBlank(src.SourceUrl) ?? DBNull.Value);
		c.Parameters.AddWithValue("p", pub is null ? DBNull.Value : pub.Value);
		c.Parameters.AddWithValue("v", (object?)NullIfBlank(src.VersionLabel) ?? DBNull.Value);
		c.Parameters.AddWithValue("h", (object?)NullIfBlank(src.ContentHash) ?? DBNull.Value);
		c.Parameters.AddWithValue("k", string.IsNullOrWhiteSpace(src.DocumentKind) ? "catalogue" : src.DocumentKind);
		c.Parameters.AddWithValue("by", uid);
		await c.ExecuteNonQueryAsync(ct);
		return id;
	}
	/// <summary>Adds category-specific attributes (AttributeDefinitions) found in the name or specs. Explicit values win; the product model itself stays generic.</summary>
	public static Dictionary<string, string> WithCategoryAttributes(string? category, string? name, IReadOnlyDictionary<string, string>? specs)
	{
		var result = ProductRules.CleanSpecs(specs);
		var text = string.Join('\n', new[] { name }.Concat(result.Select(kv => kv.Key + ": " + kv.Value)));
		foreach (var (k, v) in AttributeDefinitions.Extract(category, text))
			if (!result.ContainsKey(k) && result.Count < 40) result[k] = v;
		return result;
	}


	static string? NullIfBlank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

	static async Task SetSourceAsync(TenantSession s, Guid tid, Guid pid, Guid doc, int? page, CancellationToken ct)
	{
		await using var c = s.Command("update catalog.product set source_document_id=@d, source_page=@g where tenant_id=@t and id=@p");
		c.Parameters.AddWithValue("d", doc); c.Parameters.AddWithValue("g", page is > 0 ? page.Value : DBNull.Value); c.Parameters.AddWithValue("t", tid); c.Parameters.AddWithValue("p", pid);
		await c.ExecuteNonQueryAsync(ct);
		await using var r = s.Command("insert into catalog.product_source_reference(tenant_id,id,product_id,source_document_id,source_page) values (@t,@i,@p,@d,@g) on conflict do nothing");
		r.Parameters.AddWithValue("t", tid); r.Parameters.AddWithValue("i", Guid.NewGuid()); r.Parameters.AddWithValue("p", pid); r.Parameters.AddWithValue("d", doc); r.Parameters.AddWithValue("g", page is > 0 ? page.Value : DBNull.Value);
		await r.ExecuteNonQueryAsync(ct);
	}

	public async Task<ServiceResult<List<object>>> SourceDocumentsAsync(Guid uid, CancellationToken ct)
	{
		var tid = await db.FindTenantAsync(uid, ct);
		if (tid is null) return ServiceResult<List<object>>.Fail("No business set up.", 409);
		await using var s = await db.BeginAsync(uid, tid.Value, ct);
		await using var c = s.Command("select d.id,d.brand,d.document_name,d.source_url,d.published_on,d.version_label,d.document_kind,d.imported_at,(select count(distinct sr.product_id) from catalog.product_source_reference sr where sr.source_document_id=d.id and sr.tenant_id=d.tenant_id) from catalog.source_document d order by d.imported_at desc limit 200");
		var list = new List<object>();
		await using var r = await c.ExecuteReaderAsync(ct);
		while (await r.ReadAsync(ct)) list.Add(new { id = r.GetGuid(0), brand = r.IsDBNull(1) ? null : r.GetString(1), documentName = r.GetString(2), sourceUrl = r.IsDBNull(3) ? null : r.GetString(3), publishedOn = r.IsDBNull(4) ? null : r.GetFieldValue<DateOnly>(4).ToString("yyyy-MM-dd"), versionLabel = r.IsDBNull(5) ? null : r.GetString(5), documentKind = r.GetString(6), importedAt = r.GetFieldValue<DateTimeOffset>(7), productCount = r.GetInt64(8) });
		return ServiceResult<List<object>>.Ok(list);
	}
    static async Task<(List<ImportRowResult>, HashSet<string>)> ClassifyAsync(TenantSession s, List<ImportRowInput> rows, CancellationToken ct)
    {
        var existing = new HashSet<string>();
        await using (var c = s.Command("select dup_key from catalog.product where is_active"))
        {
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) existing.Add(r.GetString(0));
        }
        var codes = new HashSet<string>();
        await using (var c = s.Command("select code from catalog.product_barcode"))
        {
            await using var r = await c.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) codes.Add(r.GetString(0));
        }
        var seen = new HashSet<string>();
        var seenCodes = new HashSet<string>();
        var results = new List<ImportRowResult>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var err = ProductRules.Validate(row.ToInput()) ?? ProductRules.ValidateCatalogueData(row.Mrp, row.Specs);
            string? code = null;
            if (err is null && !string.IsNullOrWhiteSpace(row.Barcode))
            {
                var (norm, bErr) = BarcodeRules.Normalize(row.Barcode);
                err = bErr; code = norm;
            }
            if (err is not null) { results.Add(new(i + 1, "invalid", err)); continue; }
            var key = ProductRules.DuplicateKey(row.Name, row.Brand, row.Model);
            if (existing.Contains(key)) results.Add(new(i + 1, "duplicate", "Already in your catalogue."));
            else if (code is not null && codes.Contains(code)) results.Add(new(i + 1, "duplicate", "This barcode is already linked to a product."));
            else if (!seen.Add(key)) results.Add(new(i + 1, "duplicate_in_file", "Repeated earlier in this file."));
            else if (code is not null && !seenCodes.Add(code)) results.Add(new(i + 1, "duplicate_in_file", "This barcode is repeated earlier in this file."));
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

    /// <summary>Links the brand master and stores reference MRP/specs. Never touches price lists, GST, HSN or stock.</summary>
    static async Task SetCatalogueDataAsync(TenantSession s, Guid tid, Guid id, string? brand, decimal? mrp, Dictionary<string, string>? specs, CancellationToken ct)
    {
        var brandId = await BrandService.ResolveAsync(s, tid, brand, ct);
        var clean = ProductRules.CleanSpecs(specs);
        await using var c = s.Command("update catalog.product set brand_id=@b,mrp=coalesce(@m,mrp),specs=case when @sp::text is null then specs else @sp::jsonb end where id=@i");
        c.Parameters.AddWithValue("b", (object?)brandId ?? DBNull.Value); c.Parameters.AddWithValue("m", (object?)mrp ?? DBNull.Value);
        c.Parameters.AddWithValue("sp", clean.Count == 0 ? DBNull.Value : JsonSerializer.Serialize(clean)); c.Parameters.AddWithValue("i", id);
        await c.ExecuteNonQueryAsync(ct);
    }

    static async Task SetTaxAsync(TenantSession s, Guid id, ProductCommand m, CancellationToken ct)
    {
        await using var c = s.Command("update catalog.product set hsn_code=@h,gst_rate=@g where id=@i");
        c.Parameters.AddWithValue("h", (object?)NullIfBlank(m.HsnCode) ?? DBNull.Value);
        c.Parameters.AddWithValue("g", (object?)m.GstRate ?? DBNull.Value);
        c.Parameters.AddWithValue("i", id);
        await c.ExecuteNonQueryAsync(ct);
    }

    static string Like(string v) => v.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
