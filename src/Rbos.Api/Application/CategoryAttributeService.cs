using System.Text.Json;
using System.Text.RegularExpressions;
using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

public sealed record CategoryAttributeDefinition(
    string Key,
    string Label,
    string DataType = "text",
    string? Unit = null,
    bool IsRequired = false,
    bool IsSearchable = false,
    bool IsFilterable = false,
    int DisplayOrder = 0,
    List<string>? Options = null);

public sealed class CategoryAttributeService(TenantDb db)
{
    static readonly HashSet<string> Types = ["text", "number", "decimal", "boolean", "date", "select"];

    public async Task<ServiceResult<object>> GetAsync(Guid uid, Guid categoryId, CancellationToken ct)
    {
        var tenantId = await db.FindTenantAsync(uid, ct);
        if (tenantId is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tenantId.Value, ct);
        await using var c = s.Command(@"select d.attribute_key,d.label,d.data_type,d.unit,d.is_required,d.is_searchable,d.is_filterable,d.display_order,d.options::text
            from catalog.category_attribute_definition d
            join catalog.category c on c.tenant_id=d.tenant_id and c.id=d.category_id
            where d.tenant_id=@t and d.category_id=@c order by d.display_order,d.label");
        c.Parameters.AddWithValue("t", tenantId.Value);
        c.Parameters.AddWithValue("c", categoryId);
        var result = new List<CategoryAttributeDefinition>();
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3),
                r.GetBoolean(4), r.GetBoolean(5), r.GetBoolean(6), r.GetInt32(7),
                JsonSerializer.Deserialize<List<string>>(r.GetString(8)) ?? []));
        return ServiceResult<object>.Ok(result);
    }

    public async Task<string?> PromptGuidanceAsync(Guid uid, CancellationToken ct)
    {
        var tenantId = await db.FindTenantAsync(uid, ct);
        if (tenantId is null) return null;
        await using var s = await db.BeginAsync(uid, tenantId.Value, ct);
        await using var c = s.Command(@"select cat.name,d.attribute_key,d.label,d.unit
            from catalog.category_attribute_definition d
            join catalog.category cat on cat.tenant_id=d.tenant_id and cat.id=d.category_id
            where d.tenant_id=@t order by cat.name,d.display_order,d.label");
        c.Parameters.AddWithValue("t", tenantId.Value);
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var category = r.GetString(0);
            if (!groups.TryGetValue(category, out var fields)) groups[category] = fields = [];
            fields.Add(r.GetString(1) + " (" + r.GetString(2) + (r.IsDBNull(3) ? "" : ", unit " + r.GetString(3)) + ")");
        }
        if (groups.Count == 0) return null;
        return "Tenant-configured category attribute guidance (use when a category matches; also extract other relevant attributes when present): " +
            string.Join("; ", groups.Select(g => g.Key + ": " + string.Join(", ", g.Value)));
    }

    public async Task<ServiceResult<object>> SaveAsync(Guid uid, Guid categoryId, List<CategoryAttributeDefinition>? definitions, CancellationToken ct)
    {
        if (definitions is null || definitions.Count > 40)
            return ServiceResult<object>.Fail("Provide no more than 40 category attributes.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in definitions)
        {
            var key = (d.Key ?? "").Trim().ToLowerInvariant();
            if (!Regex.IsMatch(key, "^[a-z][a-z0-9_]{0,59}$") || !seen.Add(key))
                return ServiceResult<object>.Fail("Attribute keys must be unique snake_case identifiers (letters, numbers and underscores).");
            if (string.IsNullOrWhiteSpace(d.Label) || d.Label.Trim().Length > 80)
                return ServiceResult<object>.Fail("Every attribute needs a label of 1–80 characters.");
            if (!Types.Contains(d.DataType))
                return ServiceResult<object>.Fail("Unsupported attribute data type.");
            if (d.Unit?.Length > 20 || d.DisplayOrder is < 0 or > 1000)
                return ServiceResult<object>.Fail("Attribute unit or display order is invalid.");
            if (d.DataType == "select" && (d.Options is null || d.Options.Count == 0 || d.Options.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 80)))
                return ServiceResult<object>.Fail("Select attributes need at least one option (maximum 80 characters each).");
        }

        var tenantId = await db.FindTenantAsync(uid, ct);
        if (tenantId is null) return ServiceResult<object>.Fail("No business set up.", 409);
        await using var s = await db.BeginAsync(uid, tenantId.Value, ct);
        await using (var exists = s.Command("select 1 from catalog.category where tenant_id=@t and id=@c"))
        {
            exists.Parameters.AddWithValue("t", tenantId.Value);
            exists.Parameters.AddWithValue("c", categoryId);
            if (await exists.ExecuteScalarAsync(ct) is null) return ServiceResult<object>.Fail("Category not found.", 404);
        }
        await using (var delete = s.Command("delete from catalog.category_attribute_definition where tenant_id=@t and category_id=@c"))
        {
            delete.Parameters.AddWithValue("t", tenantId.Value);
            delete.Parameters.AddWithValue("c", categoryId);
            await delete.ExecuteNonQueryAsync(ct);
        }
        foreach (var d in definitions)
        {
            await using var insert = s.Command(@"insert into catalog.category_attribute_definition
                (tenant_id,category_id,attribute_key,label,data_type,unit,is_required,is_searchable,is_filterable,display_order,options)
                values (@t,@c,@k,@l,@dt,@u,@req,@search,@filter,@ord,@opts::jsonb)");
            insert.Parameters.AddWithValue("t", tenantId.Value);
            insert.Parameters.AddWithValue("c", categoryId);
            insert.Parameters.AddWithValue("k", d.Key.Trim().ToLowerInvariant());
            insert.Parameters.AddWithValue("l", d.Label.Trim());
            insert.Parameters.AddWithValue("dt", d.DataType);
            insert.Parameters.AddWithValue("u", (object?)d.Unit ?? DBNull.Value);
            insert.Parameters.AddWithValue("req", d.IsRequired);
            insert.Parameters.AddWithValue("search", d.IsSearchable);
            insert.Parameters.AddWithValue("filter", d.IsFilterable);
            insert.Parameters.AddWithValue("ord", d.DisplayOrder);
            insert.Parameters.AddWithValue("opts", JsonSerializer.Serialize(d.Options ?? []));
            await insert.ExecuteNonQueryAsync(ct);
        }
        await s.CommitAsync(ct);
        return ServiceResult<object>.Ok(definitions);
    }
}
