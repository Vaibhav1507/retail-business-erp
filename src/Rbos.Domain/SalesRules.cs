namespace Rbos.Domain;

public sealed record SaleLineInput(Guid? ProductId, decimal Quantity, decimal UnitPrice, decimal? Discount,
    List<string>? Serials = null, string? Barcode = null,
    string? BarcodeSymbology = null, List<string>? Imei1s = null, List<string>? Imei2s = null,
    string? Description = null, string? Brand = null, string? Model = null, string? HsnCode = null);

public sealed record SaleLine(Guid? ProductId, Quantity Quantity, decimal UnitPrice, decimal Discount, decimal LineTotal,
    string? Description = null, string? Brand = null, string? Model = null, string? HsnCode = null,
    string[]? Serials = null, string? Barcode = null,
    string? BarcodeSymbology = null, string[]? Imei1s = null, string[]? Imei2s = null)
{
    public bool IsStockTracked => ProductId.HasValue;
}

/// <summary>Pure rules for customer sales (Slice 4): validation, totals and the balanced journal.</summary>
public static class SalesRules
{
    public const int MaxLines = 100;
    public const string ReceivableAccount = "1200";
    public const string CashAccount = "1000";
    public const string RevenueAccount = "4000";
    public const string CogsAccount = "5000";
    public const string InventoryAccount = "1400";
    public static readonly string[] SaleTypes = ["Cash", "UPI", "Credit"];
    public static readonly string[] PayMethods = ["Cash", "Bank", "UPI"];

    public static bool TryValidateSale(IReadOnlyList<SaleLineInput>? input, string? saleType, decimal? amountPaid, string? payMethod, bool hasCustomer,
        DateOnly date, DateOnly today, out IReadOnlyList<SaleLine> lines, out Money total, out Money paid, out string? method, out string? error)
    {
        lines = []; total = Money.Zero; paid = Money.Zero; method = null;
        if (saleType is null || !SaleTypes.Contains(saleType)) return Fail("Sale type must be Cash, UPI or Credit.", out error);
        if (date > today) return Fail("Sale date cannot be in the future.", out error);
        if (date < today.AddYears(-5)) return Fail("Sale date is too far in the past.", out error);
        if (input is null || input.Count == 0) return Fail("At least one sale line is required.", out error);
        if (input.Count > MaxLines) return Fail($"A sale can have at most {MaxLines} lines.", out error);
        if (input.Select(l => l.ProductId).Distinct().Count() != input.Count) return Fail("Each product can appear only once per sale.", out error);

        var list = new List<SaleLine>();
        foreach (var l in input)
        {
            if (l.ProductId is null && string.IsNullOrWhiteSpace(l.Description) && string.IsNullOrWhiteSpace(l.Model))
                return Fail("An unlisted sale line needs a description or model number.", out error);
            if ((l.Description?.Trim().Length ?? 0) > 240 || (l.Brand?.Trim().Length ?? 0) > 80 ||
                (l.Model?.Trim().Length ?? 0) > 100 || (l.HsnCode?.Trim().Length ?? 0) > 12)
                return Fail("Sale line description, brand, model or HSN is too long.", out error);
            if (l.ProductId is null && ((l.Serials?.Count ?? 0) > 0 || (l.Imei1s?.Count ?? 0) > 0 ||
                (l.Imei2s?.Count ?? 0) > 0 || !string.IsNullOrWhiteSpace(l.Barcode)))
                return Fail("Serial, IMEI and barcode tracking requires a catalogue product. Add the product to the catalogue first.", out error);
            if (!Quantity.TryCreate(l.Quantity, out var q, out var qe)) return Fail(qe!, out error);
            if (l.UnitPrice <= 0 || decimal.Round(l.UnitPrice, 2) != l.UnitPrice) return Fail("Unit price must be positive with at most 2 decimal places.", out error);
            var discount = l.Discount ?? 0m;
            if (discount < 0 || decimal.Round(discount, 2) != discount) return Fail("Discount must be zero or positive with at most 2 decimal places.", out error);
            var gross = Money.Of(q.Value * l.UnitPrice).Amount;
            if (discount > gross) return Fail("Discount cannot exceed the line amount.", out error);
            var serials = (l.Serials ?? []).Select(x => (x ?? "").Trim()).Where(x => x.Length > 0).ToArray();
            if (serials.Length > q.Value) return Fail("A line cannot have more serial numbers than its quantity.", out error);
            if (serials.Any(x => x.Length is < 4 or > 40 || x.Any(char.IsWhiteSpace))) return Fail("Each serial number must be 4 to 40 characters with no spaces.", out error);
            if (serials.Distinct().Count() != serials.Length) return Fail("The same serial number is entered twice on a line.", out error);
            // IMEI1 and IMEI2 are identifiers for one physical unit, not additional serials.
            var imei1s = (l.Imei1s ?? []).Select(x => (x ?? "").Trim()).Where(x => x.Length > 0).ToArray();
            var imei2s = (l.Imei2s ?? []).Select(x => (x ?? "").Trim()).Where(x => x.Length > 0).ToArray();
            if (imei1s.Length > q.Value || imei2s.Length > q.Value)
                return Fail("There cannot be more IMEI identifiers than physical units on a sale line.", out error);
            if (imei1s.Any(x => !IsValidImei(x)) || imei2s.Any(x => !IsValidImei(x)))
                return Fail("IMEI values must contain 15 digits and pass their check digit.", out error);
            if (imei1s.Distinct().Count() != imei1s.Length || imei2s.Distinct().Count() != imei2s.Length ||
                imei1s.Intersect(imei2s, StringComparer.Ordinal).Any())
                return Fail("The same IMEI cannot appear twice on a sale line.", out error);
            list.Add(new SaleLine(l.ProductId, q, l.UnitPrice, discount, gross - discount,
                l.Description?.Trim(), l.Brand?.Trim(), l.Model?.Trim(), l.HsnCode?.Trim(),
                serials.Length == 0 ? null : serials,
                l.Barcode, l.BarcodeSymbology, imei1s.Length == 0 ? null : imei1s, imei2s.Length == 0 ? null : imei2s));
        }
        total = list.Aggregate(Money.Zero, (a, l) => a + Money.Of(l.LineTotal));
        if (total.Amount <= 0) return Fail("Sale total must be greater than zero.", out error);
        if (total.Amount > 99_999_999m) return Fail("Sale total is too large.", out error);

        if (saleType == "Credit")
        {
            if (!hasCustomer) return Fail("A credit sale needs a named customer.", out error);
            var p = amountPaid ?? 0m;
            if (p < 0 || decimal.Round(p, 2) != p) return Fail("Amount paid must be zero or positive with at most 2 decimal places.", out error);
            if (p > total.Amount) return Fail("Amount paid cannot exceed the sale total.", out error);
            paid = Money.Of(p);
            if (p > 0)
            {
                if (payMethod is null || !PayMethods.Contains(payMethod)) return Fail("Payment method must be Cash, Bank or UPI.", out error);
                method = payMethod;
            }
        }
        else
        {
            if (amountPaid is not null && amountPaid != total.Amount) return Fail("A cash or UPI sale is paid in full.", out error);
            paid = total; method = saleType;
        }
        lines = list; error = null; return true;
    }

    public static IReadOnlyList<JournalLineDraft> SaleJournal(Money total, Money paid, Money cost)
    {
        var j = new List<JournalLineDraft>();
        if (paid.Amount > 0) j.Add(new(CashAccount, paid.Amount, 0m));
        if (total.Amount - paid.Amount > 0) j.Add(new(ReceivableAccount, total.Amount - paid.Amount, 0m));
        j.Add(new(RevenueAccount, 0m, total.Amount));
        if (cost.Amount > 0) { j.Add(new(CogsAccount, cost.Amount, 0m)); j.Add(new(InventoryAccount, 0m, cost.Amount)); }
        BalancedJournalPolicy.AssertBalanced(j);
        return j;
    }

    public static bool TryValidateCustomer(string? name, string? phone, string? notes, out string? error)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120) return Fail("Customer name is required (max 120 characters).", out error);
        if ((phone?.Trim().Length ?? 0) > 30) return Fail("Phone is too long.", out error);
        if ((notes?.Trim().Length ?? 0) > 300) return Fail("Notes are too long.", out error);
        error = null; return true;
    }

    public static bool IsValidImei(string value)
    {
        if (value.Length != 15 || !value.All(char.IsAsciiDigit)) return false;
        var sum = value[^1] - '0';
        var doubleDigit = true;
        for (var i = value.Length - 2; i >= 0; i--)
        {
            var digit = value[i] - '0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
            doubleDigit = !doubleDigit;
        }
        return sum % 10 == 0;
    }

    static bool Fail(string message, out string? error) { error = message; return false; }
}
