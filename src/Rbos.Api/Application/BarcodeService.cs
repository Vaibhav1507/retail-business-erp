using Rbos.Domain;
using Npgsql;
using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

public sealed record BarcodeCommand(string? Code, string? Symbology, int? PackQuantity);

/// <summary>Barcode rules shared by lookup and create so the same physical code always maps to one stored value.</summary>
public static class BarcodeRules
{
    static readonly string[] Symbologies = ["EAN_13", "EAN_8", "UPC_A", "UPC_E", "CODE_128", "QR_CODE", "DATA_MATRIX", "MANUAL"];

    static bool GtinValid(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length - 1; i++)
        {
            var d = digits[digits.Length - 2 - i] - '0';
            sum += i % 2 == 0 ? d * 3 : d;
        }
        return (10 - sum % 10) % 10 == digits[^1] - '0';
    }

    /// <summary>Trims, validates GTIN check digits and stores UPC-A as EAN-13 (leading 0) so both scans match one record.</summary>
    public static (string? Code, string? Error) Normalize(string? raw)
    {
        var c = raw?.Trim() ?? "";
        if (c.Length is < 4 or > 64 || c.Any(char.IsWhiteSpace) || c.Any(char.IsControl)) return (null, "Enter a barcode of 4 to 64 characters without spaces.");
        if (c.All(char.IsAsciiDigit) && c.Length is 8 or 12 or 13)
        {
            if (!GtinValid(c)) return (null, "This barcode has an invalid check digit. Scan it again or retype it.");
            if (c.Length == 12) c = "0" + c;
        }
        return (c, null);
    }

    public static string? CleanSymbology(string? s) => s is not null && Symbologies.Contains(s.Trim().ToUpperInvariant()) ? s.Trim().ToUpperInvariant() : null;
}

public sealed class BarcodeService(TenantDb db)
{
    public async Task<ServiceResult<object>> LookupAsync(Guid uid, string? code, CancellationToken ct)
    {
        var (norm, err) = BarcodeRules.Normalize(code);
        if (err is not null) return ServiceResult<object>.Fail(err);
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await using var c = s.Command(@"select p.id,p.name,p.internal_sku,p.brand,p.model,p.base_unit,p.is_active,p.track_serials,b.pack_quantity,b.code,cat.name
            from catalog.product_barcode b join catalog.product p on p.tenant_id=b.tenant_id and p.id=b.product_id
            join catalog.category cat on cat.tenant_id=p.tenant_id and cat.id=p.category_id where b.code=@c");
        c.Parameters.AddWithValue("c", norm!);
        await using var r = await c.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return ServiceResult<object>.Fail("No product has this barcode.", 404);
        string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
        return ServiceResult<object>.Ok(new
        {
            productId = r.GetGuid(0), name = r.GetString(1), sku = r.GetString(2), brand = S(3), model = S(4), unit = r.GetString(5),
            isActive = r.GetBoolean(6), trackSerials = r.GetBoolean(7), packQuantity = r.GetInt32(8), code = r.GetString(9), category = r.GetString(10),
        });
    }

    public async Task<ServiceResult<object>> ListAsync(Guid uid, Guid productId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await using var c = s.Command("select id,code,symbology,pack_quantity from catalog.product_barcode where product_id=@p order by created_at");
        c.Parameters.AddWithValue("p", productId);
        var list = new List<object>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new { id = r.GetGuid(0), code = r.GetString(1), symbology = r.IsDBNull(2) ? null : r.GetString(2), packQuantity = r.GetInt32(3) });
        await r.CloseAsync();
        await using var t = s.Command("select track_serials from catalog.product where id=@p");
        t.Parameters.AddWithValue("p", productId);
        var track = await t.ExecuteScalarAsync(ct);
        if (track is null) return ServiceResult<object>.Fail("Product not found.", 404);
        return ServiceResult<object>.Ok(new { trackSerials = (bool)track, barcodes = list });
    }

    public async Task<ServiceResult<object>> AddAsync(Guid uid, Guid productId, BarcodeCommand m, CancellationToken ct)
    {
        var (norm, err) = BarcodeRules.Normalize(m.Code);
        if (err is not null) return ServiceResult<object>.Fail(err);
        var pack = m.PackQuantity ?? 1;
        if (pack is < 1 or > 100000) return ServiceResult<object>.Fail("Pack quantity must be between 1 and 100000.");
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await using (var exists = s.Command("select 1 from catalog.product where id=@p"))
        {
            exists.Parameters.AddWithValue("p", productId);
            if (await exists.ExecuteScalarAsync(ct) is null) return ServiceResult<object>.Fail("Product not found.", 404);
        }
        var id = Guid.NewGuid();
        try
        {
            await using var c = s.Command("insert into catalog.product_barcode(tenant_id,id,product_id,code,symbology,pack_quantity) values (@t,@i,@p,@c,@y,@q)");
            c.Parameters.AddWithValue("t", tid.Value); c.Parameters.AddWithValue("i", id); c.Parameters.AddWithValue("p", productId);
            c.Parameters.AddWithValue("c", norm!); c.Parameters.AddWithValue("y", (object?)BarcodeRules.CleanSymbology(m.Symbology) ?? DBNull.Value); c.Parameters.AddWithValue("q", pack);
            await c.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException e) when (e.SqlState == "23505") { return ServiceResult<object>.Fail("This barcode is already linked to a product.", 409); }
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { id, code = norm, packQuantity = pack });
    }

    /// <summary>
    /// Adds a confirmed product barcode, or confirms that it is already mapped to the
    /// same product. A barcode owned by another product is never reassigned.
    /// Must be called inside the caller's tenant-scoped transaction.
    /// </summary>
    public static async Task<bool> LinkOrConfirmAsync(
        TenantSession s, Guid tid, Guid productId, string code, string? symbology, CancellationToken ct)
    {
        var (norm, err) = BarcodeRules.Normalize(code);
        if (err is not null) return false;

        await using (var existing = s.Command(
            "select product_id from catalog.product_barcode where tenant_id=@t and code=@c"))
        {
            existing.Parameters.AddWithValue("t", tid);
            existing.Parameters.AddWithValue("c", norm!);
            var owner = await existing.ExecuteScalarAsync(ct);
            if (owner is Guid ownerId) return ownerId == productId;
        }

        await using (var insert = s.Command(@"
            insert into catalog.product_barcode(tenant_id,id,product_id,code,symbology,pack_quantity)
            values (@t,@i,@p,@c,@y,1) on conflict do nothing"))
        {
            insert.Parameters.AddWithValue("t", tid);
            insert.Parameters.AddWithValue("i", Guid.NewGuid());
            insert.Parameters.AddWithValue("p", productId);
            insert.Parameters.AddWithValue("c", norm!);
            insert.Parameters.AddWithValue("y", (object?)symbology ?? DBNull.Value);
            if (await insert.ExecuteNonQueryAsync(ct) == 1) return true;
        }

        // A concurrent request may have inserted the mapping after the first read.
        await using var verify = s.Command(
            "select product_id from catalog.product_barcode where tenant_id=@t and code=@c");
        verify.Parameters.AddWithValue("t", tid);
        verify.Parameters.AddWithValue("c", norm!);
        return await verify.ExecuteScalarAsync(ct) is Guid finalOwner && finalOwner == productId;
    }

	/// <summary>Links a barcode to a product inside an existing session; skips codes already mapped (never re-points a mapping).</summary>
	public static async Task<bool> LinkIfFreeAsync(TenantSession s, Guid tid, Guid productId, string code, CancellationToken ct)
	{
		var (norm, err) = BarcodeRules.Normalize(code);
		if (err is not null) return false;
		await using var c = s.Command("insert into catalog.product_barcode(tenant_id,id,product_id,code,pack_quantity) values (@t,@i,@p,@c,1) on conflict do nothing");
		c.Parameters.AddWithValue("t", tid); c.Parameters.AddWithValue("i", Guid.NewGuid()); c.Parameters.AddWithValue("p", productId); c.Parameters.AddWithValue("c", norm!);
		return await c.ExecuteNonQueryAsync(ct) > 0;
	}

	/// <summary>Suggestions for an unknown barcode by model number, brand and name text. Never links automatically.</summary>
	public async Task<ServiceResult<object>> SuggestAsync(Guid uid, string? code, string? model, Guid? brandId, string? text, CancellationToken ct)
	{
		var tid = await db.FindTenantAsync(uid, ct);
		if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
		var mk = IdentifierRules.ModelKey(model);
		var like = string.IsNullOrWhiteSpace(text) ? null : "%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
		await using var s = await db.BeginAsync(uid, tid.Value, ct);
		var list = new List<object>();
		if (mk.Length > 0 || like is not null || brandId is not null)
		{
			await using var c = s.Command(@"select p.id,p.name,p.brand,p.model,p.internal_sku,
					(case when @mk<>'' and p.model_norm=@mk then 3 when @mk<>'' and p.model_norm like '%'||@mk||'%' then 2 else 1 end) score
				from catalog.product p
				where p.is_active and (@br::uuid is null or p.brand_id=@br)
				  and ((@mk<>'' and p.model_norm like '%'||@mk||'%') or (@lk::text is not null and (p.name ilike @lk or p.brand ilike @lk or p.model ilike @lk)) or (@mk='' and @lk::text is null and @br::uuid is not null))
				order by score desc, p.name limit 10");
			c.Parameters.AddWithValue("mk", mk); c.Parameters.AddWithValue("br", (object?)brandId ?? DBNull.Value); c.Parameters.AddWithValue("lk", (object?)like ?? DBNull.Value);
			await using var r = await c.ExecuteReaderAsync(ct);
			while (await r.ReadAsync(ct))
				list.Add(new { id = r.GetGuid(0), name = r.GetString(1), brand = r.IsDBNull(2) ? null : r.GetString(2), model = r.IsDBNull(3) ? null : r.GetString(3), sku = r.GetString(4), match = r.GetInt32(5) == 3 ? "exact model" : r.GetInt32(5) == 2 ? "similar model" : "name/brand" });
		}
		return ServiceResult<object>.Ok(new { code, suggestions = list, externalProviders = Array.Empty<string>() });
	}

    public async Task<ServiceResult<object>> RemoveAsync(Guid uid, Guid productId, Guid barcodeId, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await using var c = s.Command("delete from catalog.product_barcode where id=@i and product_id=@p");
        c.Parameters.AddWithValue("i", barcodeId); c.Parameters.AddWithValue("p", productId);
        if (await c.ExecuteNonQueryAsync(ct) == 0) return ServiceResult<object>.Fail("Barcode not found.", 404);
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { removed = true });
    }

    /// <summary>Serial tracking is optional per product. Switching it on needs zero stock (older units have no serials); switching it off needs no in-stock serials.</summary>
    public async Task<ServiceResult<object>> SetSerialTrackingAsync(Guid uid, Guid productId, bool enabled, CancellationToken ct)
    {
        var tid = await db.FindTenantAsync(uid, ct);
        if (tid is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tid.Value, ct);
        await using (var exists = s.Command("select 1 from catalog.product where id=@p"))
        {
            exists.Parameters.AddWithValue("p", productId);
            if (await exists.ExecuteScalarAsync(ct) is null) return ServiceResult<object>.Fail("Product not found.", 404);
        }
        if (enabled)
        {
            await using var q = s.Command("select coalesce(sum(quantity),0) from inventory.stock_balance where product_id=@p");
            q.Parameters.AddWithValue("p", productId);
            if ((decimal)(await q.ExecuteScalarAsync(ct))! != 0m) return ServiceResult<object>.Fail("This product already has stock without serial numbers. Sell or adjust it to zero before turning on serial tracking.", 409);
        }
        else
        {
            await using var q = s.Command("select count(*) from inventory.serial_unit where product_id=@p and status='InStock'");
            q.Parameters.AddWithValue("p", productId);
            if ((long)(await q.ExecuteScalarAsync(ct))! > 0) return ServiceResult<object>.Fail("Units with serial numbers are still in stock. Sell or return them before turning tracking off.", 409);
        }
        await using var u = s.Command("update catalog.product set track_serials=@e where id=@p");
        u.Parameters.AddWithValue("e", enabled); u.Parameters.AddWithValue("p", productId);
        await u.ExecuteNonQueryAsync(ct);
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(new { trackSerials = enabled });
    }
}
