namespace Rbos.Domain;

public sealed record ReturnLineInput(Guid ProductId, decimal Quantity);
public sealed record SaleReturnLineInput(Guid? ProductId, Guid? SaleLineId, decimal Quantity);

/// <summary>Pure rules for customer collections and customer/supplier returns (Slice 5).</summary>
public static class AfterSalesRules
{
    public const int MaxLines = 100;
    public const string ReceivableAccount = "1200";
    public const string CashAccount = "1000";
    public const string RevenueAccount = "4000";
    public const string CogsAccount = "5000";
    public const string InventoryAccount = "1400";
    public const string PayableAccount = "2000";
    public static readonly string[] Methods = ["Cash", "Bank", "UPI"];

    public static bool TryValidateCollection(decimal amount, string? method, DateOnly date, DateOnly today, decimal owed, out Money money, out string? error)
    {
        money = Money.Zero;
        if (amount <= 0 || decimal.Round(amount, 2) != amount) return Fail("Amount must be positive with at most 2 decimal places.", out error);
        if (method is null || !Methods.Contains(method)) return Fail("Payment method must be Cash, Bank or UPI.", out error);
        if (date > today) return Fail("Payment date cannot be in the future.", out error);
        if (date < today.AddYears(-5)) return Fail("Payment date is too far in the past.", out error);
        if (owed <= 0) return Fail("This customer has nothing outstanding.", out error);
        if (amount > owed) return Fail($"Amount exceeds the outstanding balance of {owed:0.00} INR.", out error);
        money = Money.Of(amount); error = null; return true;
    }

    public static bool TryValidateSaleReturnLines(IReadOnlyList<SaleReturnLineInput>? input, string? reason,
        out IReadOnlyList<(Guid? ProductId, Guid? SaleLineId, Quantity Quantity)> lines, out string? error)
    {
        lines = [];
        if ((reason?.Trim().Length ?? 0) > 200) return Fail("Reason is too long (max 200 characters).", out error);
        if (input is null || input.Count == 0) return Fail("Choose at least one line to return.", out error);
        if (input.Count > MaxLines) return Fail($"At most {MaxLines} lines per return.", out error);
        if (input.Any(x => x.ProductId is null && x.SaleLineId is null))
            return Fail("Every returned line must identify the original sale line.", out error);
        if (input.Select(x => x.SaleLineId).Where(x => x.HasValue).Distinct().Count() != input.Count(x => x.SaleLineId.HasValue))
            return Fail("Each sale line can appear only once per return.", out error);
        var list = new List<(Guid?, Guid?, Quantity)>();
        foreach (var line in input)
        {
            if (!Quantity.TryCreate(line.Quantity, out var quantity, out var quantityError))
                return Fail(quantityError!, out error);
            list.Add((line.ProductId, line.SaleLineId, quantity));
        }
        lines = list;
        error = null;
        return true;
    }

    public static bool TryValidateReturnLines(IReadOnlyList<ReturnLineInput>? input, string? reason, out IReadOnlyList<(Guid ProductId, Quantity Quantity)> lines, out string? error)
    {
        lines = [];
        if ((reason?.Trim().Length ?? 0) > 200) return Fail("Reason is too long (max 200 characters).", out error);
        if (input is null || input.Count == 0) return Fail("Choose at least one line to return.", out error);
        if (input.Count > MaxLines) return Fail($"At most {MaxLines} lines per return.", out error);
        if (input.Select(l => l.ProductId).Distinct().Count() != input.Count) return Fail("Each product can appear only once per return.", out error);
        var list = new List<(Guid, Quantity)>();
        foreach (var l in input)
        {
            if (!Quantity.TryCreate(l.Quantity, out var q, out var qe)) return Fail(qe!, out error);
            list.Add((l.ProductId, q));
        }
        lines = list; error = null; return true;
    }

    /// <summary>Value of returned quantity at the sold net price; the final unit(s) take the remainder so totals never drift.</summary>
    public static Money ReturnedValue(decimal lineTotal, decimal soldQty, decimal alreadyQty, decimal alreadyAmount, decimal qty)
        => alreadyQty + qty == soldQty ? Money.Of(lineTotal - alreadyAmount) : Money.Of(lineTotal * qty / soldQty);

    /// <summary>Dr Cash, Cr Accounts Receivable.</summary>
    public static IReadOnlyList<JournalLineDraft> CollectionJournal(Money amount)
    {
        if (amount.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        JournalLineDraft[] j = [new(CashAccount, amount.Amount, 0m), new(ReceivableAccount, 0m, amount.Amount)];
        BalancedJournalPolicy.AssertBalanced(j);
        return j;
    }

    /// <summary>Dr Sales revenue; Cr Receivable (up to what the customer owes) and Cr Cash for any refund; Dr Inventory, Cr COGS at original cost.</summary>
    public static IReadOnlyList<JournalLineDraft> CustomerReturnJournal(Money revenue, Money receivableCredit, Money cashRefund, Money cost)
    {
        if (revenue.Amount <= 0 || receivableCredit.Amount + cashRefund.Amount != revenue.Amount) throw new ArgumentException("Return amounts do not add up.");
        var j = new List<JournalLineDraft> { new(RevenueAccount, revenue.Amount, 0m) };
        if (receivableCredit.Amount > 0) j.Add(new(ReceivableAccount, 0m, receivableCredit.Amount));
        if (cashRefund.Amount > 0) j.Add(new(CashAccount, 0m, cashRefund.Amount));
        if (cost.Amount > 0) { j.Add(new(InventoryAccount, cost.Amount, 0m)); j.Add(new(CogsAccount, 0m, cost.Amount)); }
        BalancedJournalPolicy.AssertBalanced(j);
        return j;
    }

    /// <summary>Dr Accounts Payable at the purchase cost; Cr Inventory at the cost removed; any difference goes to COGS.</summary>
    public static IReadOnlyList<JournalLineDraft> SupplierReturnJournal(Money credit, Money inventoryRemoved, Money gstReversed = default)
    {
        if (credit.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(credit));
        var j = new List<JournalLineDraft> { new(PayableAccount, credit.Amount, 0m) };
        if (inventoryRemoved.Amount > 0) j.Add(new(InventoryAccount, 0m, inventoryRemoved.Amount));
        if (gstReversed.Amount > 0) j.Add(new(PurchaseRules.GstInputAccount, 0m, gstReversed.Amount));
        var diff = credit.Amount - inventoryRemoved.Amount - gstReversed.Amount;
        if (diff > 0) j.Add(new(CogsAccount, 0m, diff));
        else if (diff < 0) j.Add(new(CogsAccount, -diff, 0m));
        BalancedJournalPolicy.AssertBalanced(j);
        return j;
    }

    static bool Fail(string message, out string? error) { error = message; return false; }
}
