using Rbos.Domain;

namespace Rbos.Api;

/// <summary>
/// Explicit access classification for every registered /api method + route template.
/// This is deliberately separate from the permission catalogue: an authenticated-member
/// exception is a valid policy, while an unknown route is never treated as one.
/// </summary>
public enum ApiAccessKind
{
    Anonymous,
    AuthenticatedUser,
    AuthenticatedMember,
    Permission
}

public sealed record ApiRouteDefinition(
    string Method,
    string Template,
    ApiAccessKind Kind,
    string? Permission = null);

public static class ApiRouteAccess
{
    private static readonly ApiRouteDefinition[] Entries =
    [
        // Public identity/bootstrap endpoints. Their endpoint handlers still apply
        // the OTP, registration and anti-enumeration rules implemented in each module.
        new("GET",  "/api/csrf", ApiAccessKind.Anonymous),
        new("GET",  "/api/auth/config", ApiAccessKind.Anonymous),
        new("POST", "/api/auth/otp/send", ApiAccessKind.Anonymous),
        new("POST", "/api/auth/otp/verify", ApiAccessKind.Anonymous),
        new("POST", "/api/auth/register", ApiAccessKind.Anonymous),
        new("POST", "/api/auth/login", ApiAccessKind.Anonymous),
        new("POST", "/api/auth/login/otp", ApiAccessKind.Anonymous),

        // Identity-only routes must work before business bootstrap and must remain
        // available for logout even when a business membership has been revoked.
        new("POST", "/api/auth/logout", ApiAccessKind.AuthenticatedUser),
        new("GET",  "/api/me", ApiAccessKind.AuthenticatedUser),
        new("POST", "/api/bootstrap", ApiAccessKind.AuthenticatedUser),

        // Public infrastructure routes mapped outside the /api group.
        new("GET", "/api/openapi/{documentName}.json", ApiAccessKind.Anonymous),
        new("GET", "/api/health", ApiAccessKind.Anonymous),
        new("*",   "/api/{**path}", ApiAccessKind.Anonymous),

        // Authenticated tenant-member routes. These preserve the previous policy
        // where the existing permission map intentionally returned no extra role
        // permission. They are explicit exceptions, not a missing-rule default.
        new("GET",  "/api/voice/intent/config", ApiAccessKind.AuthenticatedMember),
        new("POST", "/api/voice/intent", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/voice/transcribe/config", ApiAccessKind.AuthenticatedMember),
        new("POST", "/api/voice/transcribe", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/locations", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/stock", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/business-profile", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/products", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/products/master", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/categories", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/products/suggest-for-barcode", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/catalogue/source-documents", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/catalogue-sources", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/products/by-barcode", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/products/{id:guid}/barcodes", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/suppliers/{id:guid}/statement", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/suppliers/{id:guid}/open-bills", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/suppliers", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/purchases", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/purchases/{id:guid}/invoice", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/supplier-payments", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/customers", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/sales", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/sales/{id:guid}", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/business-name", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/customers/{id:guid}/statement", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/customers/{id:guid}/open-sales", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/customer-payments", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/customer-payments/{id:guid}", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/sales/{id:guid}/returnable", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/sale-returns", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/sale-returns/{id:guid}", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/purchases/{id:guid}", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/purchase-returns", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/purchase-returns/{id:guid}", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/accounts", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/money-accounts", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/money-accounts/{id:guid}/history", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/expense-categories", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/expenses", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/owner-transactions", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/loans", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/loans/{id:guid}/schedule", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/stock-adjustments", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/cash-closings", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/cash-closings/preview", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/period-lock", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/reconciliation", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/branches", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/stock-transfers", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/purchase-orders", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/unit-conversions", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/stock-count/sheet", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/stock-adjustments/history", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/stock/low", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/stock/dead", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/stock/ageing", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/customers/{id:guid}/history", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/customers/ageing", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/reminders", ApiAccessKind.AuthenticatedMember),
        new("GET",  "/api/brands", ApiAccessKind.AuthenticatedMember),

        // Explicit named permissions. Role bundles remain unchanged.
        new("PUT",  "/api/profile/mobile/send-otp", ApiAccessKind.Permission, Permissions.SettingsManage),
        new("POST", "/api/profile/mobile/send-otp", ApiAccessKind.Permission, Permissions.SettingsManage),
        new("POST", "/api/profile/mobile/verify", ApiAccessKind.Permission, Permissions.SettingsManage),
        new("PUT",  "/api/business-profile", ApiAccessKind.Permission, Permissions.SettingsManage),
        new("PUT",  "/api/period-lock", ApiAccessKind.Permission, Permissions.SettingsManage),
        new("POST", "/api/branches", ApiAccessKind.Permission, Permissions.SettingsManage),
        new("POST", "/api/locations", ApiAccessKind.Permission, Permissions.SettingsManage),

        new("POST", "/api/products/master/{id:guid}/add", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("POST", "/api/products", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("PUT",  "/api/products/{id:guid}", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("PUT",  "/api/categories/{id:guid}", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("POST", "/api/products/import/read", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("POST", "/api/products/import/preview", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("POST", "/api/products/import/commit", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("POST", "/api/products/{id:guid}/barcodes", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("DELETE", "/api/products/{id:guid}/barcodes/{barcodeId:guid}", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("PUT",  "/api/products/{id:guid}/serial-tracking", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("PUT",  "/api/unit-conversions", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("PUT",  "/api/reorder-levels", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("POST", "/api/brands", ApiAccessKind.Permission, Permissions.CatalogueManage),
        new("PUT",  "/api/brands/{id:guid}", ApiAccessKind.Permission, Permissions.CatalogueManage),

        new("POST", "/api/suppliers", ApiAccessKind.Permission, Permissions.PurchasesManage),
        new("PUT",  "/api/suppliers/{id:guid}", ApiAccessKind.Permission, Permissions.PurchasesManage),
        new("POST", "/api/purchases", ApiAccessKind.Permission, Permissions.PurchasesManage),
        new("POST", "/api/purchases/extract", ApiAccessKind.Permission, Permissions.PurchasesManage),
        new("POST", "/api/supplier-payments", ApiAccessKind.Permission, Permissions.PurchasesManage),
        new("POST", "/api/purchase-returns", ApiAccessKind.Permission, Permissions.PurchasesManage),
        new("POST", "/api/purchase-orders", ApiAccessKind.Permission, Permissions.PurchasesManage),
        new("PUT",  "/api/purchase-orders/{id:guid}/status", ApiAccessKind.Permission, Permissions.PurchasesManage),

        new("POST", "/api/sales/{id:guid}/invoice/whatsapp/retry", ApiAccessKind.Permission, Permissions.SalesCreate),
        new("POST", "/api/sales", ApiAccessKind.Permission, Permissions.SalesCreate),
        new("POST", "/api/customers", ApiAccessKind.Permission, Permissions.CustomersManage),
        new("PUT",  "/api/customers/{id:guid}", ApiAccessKind.Permission, Permissions.CustomersManage),
        new("POST", "/api/customer-payments", ApiAccessKind.Permission, Permissions.CustomersManage),
        new("POST", "/api/sales/{id:guid}/cancel", ApiAccessKind.Permission, Permissions.SalesCancel),
        new("POST", "/api/sale-returns", ApiAccessKind.Permission, Permissions.SalesReturn),

        new("POST", "/api/opening-stock", ApiAccessKind.Permission, Permissions.StockAdjust),
        new("POST", "/api/stock-transfers", ApiAccessKind.Permission, Permissions.StockAdjust),
        new("POST", "/api/stock-count", ApiAccessKind.Permission, Permissions.StockAdjust),

        new("POST", "/api/accounts", ApiAccessKind.Permission, Permissions.FinanceManage),
        new("PUT",  "/api/accounts/{id:guid}/active", ApiAccessKind.Permission, Permissions.FinanceManage),
        new("PUT",  "/api/accounts/{id:guid}", ApiAccessKind.Permission, Permissions.FinanceManage),
        new("POST", "/api/money-accounts", ApiAccessKind.Permission, Permissions.FinanceManage),
        new("POST", "/api/expenses", ApiAccessKind.Permission, Permissions.FinanceManage),
        new("POST", "/api/owner-transactions", ApiAccessKind.Permission, Permissions.FinanceManage),
        new("POST", "/api/loans", ApiAccessKind.Permission, Permissions.FinanceManage),
        new("POST", "/api/loans/{id:guid}/repayments", ApiAccessKind.Permission, Permissions.FinanceManage),
        new("POST", "/api/stock-adjustments", ApiAccessKind.Permission, Permissions.FinanceManage),
        new("POST", "/api/cash-closings", ApiAccessKind.Permission, Permissions.FinanceManage),
        new("POST", "/api/reconciliation/clear", ApiAccessKind.Permission, Permissions.FinanceManage),

        new("GET",  "/api/reports/stock-verification", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/reports/trial-balance", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/reports/ledger/{accountId:guid}", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/reports/profit-loss", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/reports/balance-sheet", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/reports/receivable-ageing", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/reports/payable-ageing", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/reports/stock-valuation", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/reports/cash-flow", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/reports/wac-verification", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/dashboard", ApiAccessKind.Permission, Permissions.ReportsView),
        new("GET",  "/api/audit", ApiAccessKind.Permission, Permissions.AuditView),

        // These two operations keep their prior authenticated-member behaviour:
        // one records an export event; it does not return the exported report itself.
        new("POST", "/api/exports/audit", ApiAccessKind.AuthenticatedMember),

        // All staff directory and staff role catalogue operations require staff.manage.
        new("GET",  "/api/staff/roles", ApiAccessKind.Permission, Permissions.StaffManage),
        new("GET",  "/api/staff", ApiAccessKind.Permission, Permissions.StaffManage),
        new("POST", "/api/staff", ApiAccessKind.Permission, Permissions.StaffManage),
        new("PUT",  "/api/staff/{userId:guid}", ApiAccessKind.Permission, Permissions.StaffManage)
    ];

    private static readonly IReadOnlyDictionary<string, ApiRouteDefinition> ByKey =
        Entries.ToDictionary(x => Key(x.Method, x.Template), StringComparer.OrdinalIgnoreCase);

    /// <summary>All explicit method/template classifications, exposed for route-inventory tests and docs.</summary>
    public static IReadOnlyList<ApiRouteDefinition> Definitions => Entries;

    /// <summary>Returns null only when a method/template has not been explicitly classified.</summary>
    public static ApiRouteDefinition? Resolve(string method, string? routeTemplate)
    {
        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(routeTemplate))
            return null;

        if (ByKey.TryGetValue(Key(method, routeTemplate), out var exact))
            return exact;

        return ByKey.TryGetValue(Key("*", routeTemplate), out var wildcard) ? wildcard : null;
    }

    private static string Key(string method, string template) => $"{method.Trim().ToUpperInvariant()} {template.Trim()}";
}
