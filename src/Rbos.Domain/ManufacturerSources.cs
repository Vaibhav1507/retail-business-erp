namespace Rbos.Domain;

/// <summary>
/// Manufacturer Catalogue Source Registry (reference only). Availability reflects what was actually checked on LastChecked
/// (homepage + robots.txt reachability only). CatalogueDiscovered, DownloadableFileAvailable and ProductPagesAvailable stay false until a specific catalogue/page is actually verified. No downloadable catalogue URL is listed unless verified; none were verified,
/// so users upload official files they download themselves. Respect each site's terms and copyright.
/// </summary>
public sealed record ManufacturerSource(string Brand, string Category, string SourceUrl, string SourceType, string Availability, string LastChecked,
    bool OfficialWebsiteVerified, bool CatalogueDiscovered, bool DownloadableFileAvailable, bool ProductPagesAvailable, bool ManualUploadRequired, bool AccessRestricted);

public static class ManufacturerSources
{
    const string D = "2026-11-10";
    const string Pages = "official product pages";
    const string Note = "; downloadable catalogue not verified, upload files you download manually";
    public static readonly IReadOnlyList<ManufacturerSource> All =
    [
        new("LG", "Electronics & appliances", "https://www.lg.com/in/", Pages, "Reachable; robots.txt allows general crawling, search pages restricted" + Note, D, true, false, false, false, true, true),
        new("Samsung", "Electronics & appliances", "https://www.samsung.com/in/", Pages, "Reachable; robots.txt present" + Note, D, true, false, false, false, true, false),
        new("Whirlpool", "Home appliances", "https://www.whirlpoolindia.com/", Pages, "Reachable; account/search paths disallowed" + Note, D, true, false, false, false, true, true),
        new("Daikin", "Air conditioners", "https://www.daikinindia.com/", Pages, "Reachable; robots.txt allows all" + Note, D, true, false, false, false, true, false),
        new("Blue Star", "Air conditioners & cooling", "https://www.bluestarindia.com/", Pages, "Reachable; robots.txt allows all" + Note, D, true, false, false, false, true, false),
        new("Voltas", "Air conditioners & appliances", "https://www.voltas.com/", Pages, "Reachable; robots.txt allows PDFs, download paths disallowed" + Note, D, true, false, false, false, true, true),
        new("Godrej", "Home appliances", "https://www.godrejappliances.com/", Pages, "Unverified: domain did not resolve and godrej.com returned 403 on this check", D, false, false, false, false, true, true),
        new("Haier", "Home appliances", "https://www.haier.com/in/", Pages, "Unverified: India site not confirmed (global robots.txt reachable)", D, true, false, false, false, true, true),
        new("Panasonic", "Electronics & appliances", "https://www.panasonic.com/in/", Pages, "Unavailable to automated check: HTTP 403", D, false, false, false, false, true, true),
        new("IFB", "Washing machines & kitchen", "https://www.ifbappliances.com/", Pages, "Reachable; robots.txt present" + Note, D, true, false, false, false, true, false),
        new("Bosch", "Home appliances", "https://www.bosch-home.in/", Pages, "Reachable via redirect (HTTP 308); robots.txt present" + Note, D, true, false, false, false, true, false),
        new("Havells", "Electricals & appliances", "https://www.havells.com/", Pages, "Reachable; catalogue/checkout paths restricted in robots.txt" + Note, D, true, false, false, false, true, true),
        new("Crompton", "Fans, lighting & appliances", "https://www.crompton.co.in/", Pages, "Reachable; robots.txt allows general crawling" + Note, D, true, false, false, false, true, false),
    ];
}
