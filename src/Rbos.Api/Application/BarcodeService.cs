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
