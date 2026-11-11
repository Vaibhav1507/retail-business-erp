namespace Rbos.Domain;

public sealed record ProductInput(string? Name, string? CategoryName, bool IsGeneric, string? BaseUnit, string? Brand, string? Model, IReadOnlyDictionary<string, string>? Attributes);

public static class ProductRules
{
    public static readonly string[] Units = ["pcs", "set", "pack", "m"];
    public static readonly string[] AttributeKeys = ["colour", "capacity", "connector_type", "compatible_with"];
    public const int MaxImportRows = 200;

    /// <summary>Lower-case, letters and digits only. Mirrors catalog.norm_text in migration 006.</summary>
    public static string Normalize(string? v) =>
        new(string.IsNullOrWhiteSpace(v) ? [] : v.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    public static string DuplicateKey(string? name, string? brand, string? model) =>
        $"{Normalize(name)}|{Normalize(brand)}|{Normalize(model)}";

    public static string FormatSku(long n) => $"INT-{n:D6}";

    /// <summary>Returns the first validation error, or null when the input is valid.</summary>
    public static string? Validate(ProductInput p)
    {
        if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Trim().Length > 160) return "Name is required (max 160 characters).";
        if (string.IsNullOrWhiteSpace(p.CategoryName) || p.CategoryName.Trim().Length > 80) return "Category is required (max 80 characters).";
        if (p.BaseUnit is null || !Units.Contains(p.BaseUnit)) return "Unit must be pcs, set, pack or m.";
        if (p.IsGeneric && (!string.IsNullOrWhiteSpace(p.Brand) || !string.IsNullOrWhiteSpace(p.Model))) return "Generic products cannot have brand or model.";
        if ((p.Brand?.Trim().Length ?? 0) > 80 || (p.Model?.Trim().Length ?? 0) > 80) return "Brand and model are limited to 80 characters.";
        if (p.Attributes is not null)
        {
            if (p.Attributes.Keys.Any(k => !AttributeKeys.Contains(k))) return "Unsupported attribute key.";
            if (p.Attributes.Values.Any(v => v is null || v.Length > 100)) return "Attribute values are limited to 100 characters.";
        }
        return null;
    }

    public static string? ValidateCatalogueData(decimal? mrp, IReadOnlyDictionary<string, string>? specs)
    {
        if (mrp is < 0 or > 100000000) return "MRP must be between 0 and 100,000,000.";
        if (specs is not null && (specs.Count > 40 || specs.Any(kv => kv.Key.Trim().Length is 0 or > 60 || (kv.Value?.Length ?? 0) > 200))) return "Specifications: at most 40 entries, names up to 60 and values up to 200 characters.";
        return null;
    }

    public static Dictionary<string, string> CleanSpecs(IReadOnlyDictionary<string, string>? specs) =>
        (specs ?? new Dictionary<string, string>()).Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            .GroupBy(kv => kv.Key.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Value.Trim());

    /// <summary>Drops blank attribute values and trims the rest.</summary>
    public static Dictionary<string, string> CleanAttributes(IReadOnlyDictionary<string, string>? a) =>
        (a ?? new Dictionary<string, string>()).Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value.Trim());
}
