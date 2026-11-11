using System.Text.RegularExpressions;

namespace Rbos.Domain;

/// <summary>A category-specific product attribute, extracted from catalogue text into product specs.</summary>
public sealed record AttributeDefinition(string Key, string Label, string[] CategoryHints, string[] Aliases, string? Unit = null);

/// <summary>Reusable, brand-neutral attribute definitions. Categories match by case-insensitive substring of the category name; an empty hint list applies to all.</summary>
public static class AttributeDefinitions
{
    public static readonly IReadOnlyList<AttributeDefinition> All =
    [
        new("capacity_kg", "Capacity (kg)", ["washing", "washer"], ["capacity", "wash capacity", "load capacity"], "kg"),
        new("capacity_litres", "Capacity (litres)", ["refrigerator", "freezer", "fridge", "microwave"], ["capacity", "net capacity", "gross capacity"], "L"),
        new("energy_rating", "Energy rating", ["washing", "refrigerator", "freezer", "air condition", "fridge"], ["energy rating", "star rating", "bee rating", "star"]),
        new("motor_type", "Motor type", ["washing", "air condition"], ["motor", "motor type", "compressor"]),
        new("spin_speed_rpm", "Spin speed (rpm)", ["washing"], ["spin speed", "max spin", "rpm"], "rpm"),
        new("tonnage", "Tonnage", ["air condition", "ac"], ["tonnage", "capacity (ton)", "ton"], "ton"),
        new("screen_size_inch", "Screen size (inch)", ["tv", "television", "monitor"], ["screen size", "display size"], "inch"),
        new("resolution", "Resolution", ["tv", "television", "monitor"], ["resolution", "display resolution"]),
        new("warranty", "Warranty", [], ["warranty", "product warranty"]),
        new("power_w", "Power (W)", [], ["power", "wattage", "power consumption"], "W"),
    ];

    public static IEnumerable<AttributeDefinition> ForCategory(string? category) =>
        All.Where(d => d.CategoryHints.Length == 0 || (!string.IsNullOrWhiteSpace(category) && d.CategoryHints.Any(h => category.Contains(h, StringComparison.OrdinalIgnoreCase))));

    /// <summary>Finds "alias: value" or "alias value" pairs in free text for the category's definitions. Returns key to value.</summary>
    public static Dictionary<string, string> Extract(string? category, string text)
    {
        var found = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(text)) return found;
        foreach (var d in ForCategory(category))
            foreach (var alias in d.Aliases.OrderByDescending(a => a.Length))
            {
                var m = Regex.Match(text, @"\b" + Regex.Escape(alias) + @"\b\s*[:\-]?\s*([A-Za-z0-9][A-Za-z0-9 ./+%()]{0,30}?)(?=\s{2,}|[,;\n]|$)", RegexOptions.IgnoreCase);
                if (m.Success && m.Groups[1].Value.Trim().Length > 0) { found[d.Key] = m.Groups[1].Value.Trim(); break; }
            }
        return found;
    }
}
