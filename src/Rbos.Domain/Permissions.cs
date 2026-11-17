namespace Rbos.Domain;

/// <summary>Operations that can be authorised. Roles are only bundles of these permissions.</summary>
public static class Permissions
{
    public const string SalesCreate = "sales.create";
    public const string SalesCancel = "sales.cancel";
    public const string SalesReturn = "sales.return";
    public const string CustomersManage = "customers.manage";
    public const string CatalogueManage = "catalogue.manage";
    public const string StockAdjust = "stock.adjust";
    public const string PurchasesManage = "purchases.manage";
    public const string FinanceManage = "finance.manage";
    public const string ReportsView = "reports.view";
    public const string SettingsManage = "settings.manage";
    public const string StaffManage = "staff.manage";
    public const string AuditView = "audit.view";

    public static readonly IReadOnlyList<string> All =
    [
        SalesCreate, SalesCancel, SalesReturn, CustomersManage, CatalogueManage, StockAdjust,
        PurchasesManage, FinanceManage, ReportsView, SettingsManage, StaffManage, AuditView,
    ];

    private static readonly Dictionary<string, string[]> ByRole = new(StringComparer.Ordinal)
    {
        ["Owner"] = [.. All],
        ["Manager"] = [SalesCreate, SalesCancel, SalesReturn, CustomersManage, CatalogueManage, StockAdjust, PurchasesManage, ReportsView, AuditView],
        ["Accountant"] = [FinanceManage, ReportsView, AuditView],
        ["Cashier"] = [SalesCreate, CustomersManage],
    };

    public static IReadOnlyList<string> Roles => [.. ByRole.Keys];
    public static bool IsKnownRole(string? role) => role is not null && ByRole.ContainsKey(role);
    public static IReadOnlyList<string> For(string? role) => role is not null && ByRole.TryGetValue(role, out var permissions) ? permissions : [];
    public static bool Has(string? role, string permission) => For(role).Contains(permission);
}
