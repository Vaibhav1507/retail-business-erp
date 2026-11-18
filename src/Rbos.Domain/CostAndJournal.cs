namespace Rbos.Domain;

/// <summary>Running cost state for one product across all locations.</summary>
public readonly record struct CostState(decimal TotalQuantity, decimal InventoryValue, decimal Wac)
{
	public static CostState Empty => new(0m, 0m, 0m);
}

/// <summary>
/// Weighted average cost for the Slice 1 subset only: receipts at a known positive cost.
/// Sales, returns, cost correction and revaluation are not implemented.
/// </summary>
public static class WeightedAverageCostCalculator
{
    /// <summary>Interim persisted WAC scale: inventory.item_cost.wac is numeric(18,4).
    /// This engineering convention is not CA approval of the valuation policy.</summary>
    public const int WacDecimalPlaces = 4;

    public static decimal RoundWac(decimal value) =>
        decimal.Round(value, WacDecimalPlaces, MidpointRounding.AwayFromZero);

	public static CostState ApplyReceipt(CostState current, Quantity quantity, UnitCost unitCost)
	{
		var value = Money.Of(quantity.Value * unitCost.Value).Amount;
		var totalQuantity = current.TotalQuantity + quantity.Value;
		var inventoryValue = current.InventoryValue + value;
		var wac = RoundWac(inventoryValue / totalQuantity);
		return new CostState(totalQuantity, inventoryValue, wac);
	}
}

public sealed record JournalLineDraft(string AccountCode, decimal Debit, decimal Credit);

public static class BalancedJournalPolicy
{
	public const string InventoryAccount = "1400";
	public const string OpeningBalanceEquityAccount = "3900";

	public static IReadOnlyList<JournalLineDraft> OpeningStock(Money total)
	{
		if (total.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(total), "Journal total must be positive.");
		var lines = new[]
		{
			new JournalLineDraft(InventoryAccount, total.Amount, 0m),
			new JournalLineDraft(OpeningBalanceEquityAccount, 0m, total.Amount),
		};
		AssertBalanced(lines);
		return lines;
	}

	public static void AssertBalanced(IReadOnlyCollection<JournalLineDraft> lines)
	{
		if (lines.Count < 2) throw new InvalidOperationException("A journal needs at least two lines.");
		if (lines.Any(l => l.Debit < 0 || l.Credit < 0 || (l.Debit > 0) == (l.Credit > 0)))
			throw new InvalidOperationException("Each journal line must have exactly one positive side.");
		if (lines.Sum(l => l.Debit) != lines.Sum(l => l.Credit))
			throw new InvalidOperationException("Journal is not balanced.");
	}
}
