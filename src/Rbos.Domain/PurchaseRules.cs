namespace Rbos.Domain;

public sealed record PurchaseLineInput(Guid ProductId, decimal Quantity, decimal UnitCost);

public sealed record PurchaseLine(Guid ProductId, Quantity Quantity, UnitCost UnitCost)
{
    public Money Value => Money.Of(Quantity.Value * UnitCost.Value);
}

/// <summary>Pure rules for purchase receipts and supplier payments (Slice 3).</summary>
public static class PurchaseRules
{
    public const int MaxLines = 100;
    public const string PayableAccount = "2000";
    public const string CashAccount = "1000";
    public static readonly string[] PaymentMethods = ["Cash", "Bank", "UPI"];

    public static bool TryValidateReceipt(IReadOnlyList<PurchaseLineInput>? input, DateOnly date, DateOnly today, out IReadOnlyList<PurchaseLine> lines, out string? error)
    {
        lines = [];
        error = null;
        if (input is null || input.Count == 0) { error = "Add at least one line."; return false; }
        if (input.Count > MaxLines) { error = $"At most {MaxLines} lines per receipt."; return false; }
        if (date > today) { error = "Receipt date cannot be in the future."; return false; }
        if (date < today.AddYears(-5)) { error = "Receipt date is too old."; return false; }
        var list = new List<PurchaseLine>();
        foreach (var l in input)
        {
            if (!Quantity.TryCreate(l.Quantity, out var q, out var qe)) { error = qe; return false; }
            if (!UnitCost.TryCreate(l.UnitCost, out var c, out var ce)) { error = ce; return false; }
            if (list.Any(x => x.ProductId == l.ProductId)) { error = "A product can appear only once per receipt."; return false; }
            list.Add(new PurchaseLine(l.ProductId, q, c));
        }
        lines = list;
        return true;
    }

    public static Money Total(IReadOnlyList<PurchaseLine> lines) => lines.Aggregate(Money.Zero, (a, l) => a + l.Value);

    /// <summary>Dr Inventory, Cr Accounts Payable.</summary>
    public static IReadOnlyList<JournalLineDraft> ReceiptJournal(Money total)
    {
        if (total.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(total));
        JournalLineDraft[] j = [new(BalancedJournalPolicy.InventoryAccount, total.Amount, 0m), new(PayableAccount, 0m, total.Amount)];
        BalancedJournalPolicy.AssertBalanced(j);
        return j;
    }

    /// <summary>Dr Accounts Payable, Cr Cash/Bank.</summary>
    public static IReadOnlyList<JournalLineDraft> PaymentJournal(Money amount)
    {
        if (amount.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        JournalLineDraft[] j = [new(PayableAccount, amount.Amount, 0m), new(CashAccount, 0m, amount.Amount)];
        BalancedJournalPolicy.AssertBalanced(j);
        return j;
    }

    public static bool TryValidatePayment(decimal amount, string? method, DateOnly date, DateOnly today, decimal outstanding, out Money money, out string? error)
    {
        money = Money.Zero;
        error = null;
        if (amount <= 0 || Money.Round(amount) != amount) error = "Amount must be greater than zero with at most 2 decimal places.";
        else if (amount > 100_000_000m) error = "Amount is too large.";
        else if (method is null || !PaymentMethods.Contains(method)) error = "Method must be Cash, Bank or UPI.";
        else if (date > today) error = "Payment date cannot be in the future.";
        else if (date < today.AddYears(-5)) error = "Payment date is too old.";
        else if (amount > outstanding) error = $"Payment exceeds the amount owed ({outstanding:0.00}).";
        else money = Money.Of(amount);
        return error is null;
    }

    public static bool TryValidateSupplier(string? name, string? phone, string? notes, out string? error)
    {
        error = null;
        var n = name?.Trim() ?? "";
        if (n.Length is < 1 or > 120) error = "Supplier name is required (max 120 characters).";
        else if ((phone?.Trim().Length ?? 0) > 30) error = "Phone is too long.";
        else if ((notes?.Trim().Length ?? 0) > 300) error = "Notes are too long.";
        return error is null;
    }
}
