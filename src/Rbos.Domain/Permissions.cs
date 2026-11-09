namespace Rbos.Domain;

/// <summary>D2: operations that can be authorised. Roles are only bundles of these; endpoints check permissions, never role names.</summary>
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

    static readonly Dictionary<string, string[]> ByRole = new(StringComparer.Ordinal)
    {
        ["Owner"] = [.. All],
        ["Manager"] = [SalesCreate, SalesCancel, SalesReturn, CustomersManage, CatalogueManage, StockAdjust, PurchasesManage, ReportsView, AuditView],
        ["Accountant"] = [FinanceManage, ReportsView, AuditView],
        ["Cashier"] = [SalesCreate, CustomersManage],
    };

    public static IReadOnlyList<string> Roles => [.. ByRole.Keys];
    public static bool IsKnownRole(string? role) => role is not null && ByRole.ContainsKey(role);
    public static IReadOnlyList<string> For(string? role) => role is not null && ByRole.TryGetValue(role, out var p) ? p : [];
    public static bool Has(string? role, string permission) => For(role).Contains(permission);

    /// <summary>Maps an API route to the permission it needs, or null when any signed-in member may call it.</summary>
    public static string? Required(string method, string path)
    {
        var get = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
        bool Is(string prefix) => path.StartsWith("/api" + prefix, StringComparison.OrdinalIgnoreCase);

        if (Is("/staff")) return StaffManage;
        if (Is("/branches") || (Is("/locations") && !get)) return get ? null : SettingsManage;
        if (Is("/audit")) return AuditView;
        if (Is("/reports") || Is("/dashboard")) return ReportsView;
        if (Is("/business-profile") || Is("/period-lock") || Is("/profile")) return get ? null : SettingsManage;
        if (Is("/sales") && path.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase)) return SalesCancel;
        if (Is("/sale-returns")) return get ? null : SalesReturn;
        if (Is("/sales")) return get ? null : SalesCreate;
        if (Is("/customers") || Is("/customer-payments")) return get ? null : CustomersManage;
        if (Is("/products") || Is("/categories") || Is("/unit-conversions") || Is("/reorder-levels")) return get ? null : CatalogueManage;
        if (Is("/stock-adjustments") || Is("/stock-transfers") || Is("/stock-count") || Is("/opening-stock")) return get ? null : StockAdjust;
        if (Is("/purchases") || Is("/purchase-orders") || Is("/purchase-returns") || Is("/suppliers") || Is("/supplier-payments")) return get ? null : PurchasesManage;
        if (Is("/expenses") || Is("/reconciliation") || Is("/expense-categories") || Is("/money-accounts") || Is("/owner-transactions") || Is("/loans") || Is("/accounts") || Is("/cash-closings")) return get ? null : FinanceManage;
        return null;
    }
}
