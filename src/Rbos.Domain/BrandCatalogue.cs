namespace Rbos.Domain;

public sealed record SeedBrand(string Name, string? Website, string[] Aliases);

/// <summary>
/// Starter brand list for Indian electronics and appliance shops. Names and aliases come from common trade usage.
/// Websites are the brands' India or global home pages; where the address was not confirmed it is left empty
/// so the shop owner can fill it in. Seeding never overwrites a brand the shop has already edited.
/// </summary>
public static class BrandCatalogue
{
    public static readonly IReadOnlyList<SeedBrand> Seed =
    [
        new("LG", "https://www.lg.com/in", ["L.G.", "LG Electronics"]),
        new("Samsung", "https://www.samsung.com/in", []),
        new("Whirlpool", "https://www.whirlpoolindia.com", []),
        new("Daikin", "https://www.daikinindia.com", []),
        new("Blue Star", "https://www.bluestarindia.com", ["Bluestar"]),
        new("Voltas", "https://www.voltas.com", []),
        new("Godrej", "https://www.godrejappliances.com", ["Godrej Appliances"]),
        new("Haier", "https://www.haierindia.com", []),
        new("Panasonic", "https://www.panasonic.com/in", []),
        new("IFB", "https://www.ifbappliances.com", ["IFB Industries"]),
        new("Bosch", "https://www.bosch-home.in", []),
        new("Havells", "https://havells.com", []),
        new("Crompton", "https://www.crompton.co.in", ["Crompton Greaves"]),
        new("Philips", "https://www.philips.co.in", []),
        new("Bajaj", "https://www.bajajelectricals.com", ["Bajaj Electricals"]),
        new("Sony", "https://www.sony.co.in", []),
        new("Carrier", "https://www.carrier.com/residential/en/in", []),
        new("Mitsubishi Electric", "https://www.mitsubishielectric.in", ["Mitsubishi"]),
        new("Hitachi", null, []),
        new("Lloyd", null, []),
        new("Toshiba", null, []),
        new("Electrolux", "https://www.electrolux.in", []),
        new("Hisense", null, []),
        new("TCL", "https://www.tcl.com/in", []),
        new("Onida", null, []),
        new("Sansui", null, []),
        new("Usha", "https://www.usha.com", []),
        new("Orient Electric", "https://www.orientelectric.com", ["Orient"]),
        new("Symphony", "https://www.symphonylimited.com", []),
        new("Atomberg", "https://atomberg.com", []),
        new("Polycab", "https://www.polycab.com", []),
        new("V-Guard", "https://www.vguard.in", ["VGuard"]),
        new("Luminous", "https://www.luminousindia.com", []),
        new("Microtek", null, []),
        new("Syska", null, []),
        new("Kent", "https://www.kent.co.in", ["Kent RO"]),
        new("Eureka Forbes", "https://www.eurekaforbes.com", ["Aquaguard"]),
        new("Prestige", "https://www.ttkprestige.com", ["TTK Prestige"]),
        new("Preethi", "https://www.preethi.in", []),
        new("Butterfly", null, []),
        new("Dell", "https://www.dell.com/en-in", []),
        new("HP", "https://www.hp.com/in-en", ["Hewlett-Packard", "Hewlett Packard"]),
        new("Lenovo", "https://www.lenovo.com/in/en", []),
        new("Asus", "https://www.asus.com/in", ["ASUS"]),
        new("Acer", "https://www.acer.com/in-en", []),
        new("Xiaomi", "https://www.mi.com/in", ["Mi", "Redmi"]),
        new("Realme", "https://www.realme.com/in", []),
        new("OnePlus", "https://www.oneplus.in", ["One Plus"]),
        new("Vivo", "https://www.vivo.com/in", []),
        new("Oppo", "https://www.oppo.com/in", []),
        new("boAt", "https://www.boat-lifestyle.com", ["Boat"]),
        new("JBL", "https://in.jbl.com", []),
    ];
}

public static class BrandRules
{
    public const int MaxName = 80;

    /// <summary>Same rule as catalog.norm_text, so a brand matches however it is spaced or punctuated.</summary>
    public static string Key(string? name) => ProductRules.Normalize(name);

    public static string? Validate(string? name, string? website, IEnumerable<string>? aliases)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxName || Key(name).Length == 0) return "Brand name is required (max 80 characters).";
        if (!string.IsNullOrWhiteSpace(website) && (website.Trim().Length > 200 || !(website.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase) || website.Trim().StartsWith("http://", StringComparison.OrdinalIgnoreCase)))) return "Website must start with http:// or https:// (max 200 characters).";
        if (aliases is not null && aliases.Any(a => a.Trim().Length > MaxName)) return "Each alias is limited to 80 characters.";
        return null;
    }

    public static List<string> CleanAliases(IEnumerable<string>? aliases, string name)
    {
        var self = Key(name);
        return (aliases ?? []).Select(a => a.Trim()).Where(a => a.Length > 0 && Key(a).Length > 0 && Key(a) != self)
            .GroupBy(Key).Select(g => g.First()).Take(20).ToList();
    }
}

public static class IdentifierRules
{
    /// <summary>GTIN check digit for 8, 12, 13 or 14 digit codes (EAN-8, UPC-A, EAN-13, GTIN-14).</summary>
    public static bool GtinValid(string digits)
    {
        if (digits.Length is not (8 or 12 or 13 or 14) || !digits.All(char.IsAsciiDigit)) return false;
        var sum = 0;
        for (var i = 0; i < digits.Length - 1; i++)
        {
            var d = digits[digits.Length - 2 - i] - '0';
            sum += i % 2 == 0 ? d * 3 : d;
        }
        return (10 - sum % 10) % 10 == digits[^1] - '0';
    }

    /// <summary>
    /// A manufacturer model number such as "GL-S292RPZY" identifies a product design. It is compared ignoring case, spaces and
    /// hyphens, but the original spelling is always what is stored and shown. Serial numbers and barcodes are never compared here.
    /// </summary>
    public static string ModelKey(string? model) => ProductRules.Normalize(model);

    public static bool LooksLikeBarcode(string? input)
    {
        var c = input?.Trim() ?? "";
        return c.Length >= 8 && c.All(char.IsAsciiDigit) && GtinValid(c);
    }
}
