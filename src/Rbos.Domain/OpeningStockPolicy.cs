namespace Rbos.Domain;

public sealed record OpeningStockLineInput(Guid ProductId, decimal Quantity, decimal UnitCost);

public sealed record OpeningStockLine(Guid ProductId, Quantity Quantity, UnitCost UnitCost)
{
	public Money Value => Money.Of(Quantity.Value * UnitCost.Value);
}

/// <summary>Validates an opening-stock command. Pure: no HTTP, no database.</summary>
public static class OpeningStockPolicy
{
	public const int MaxLines = 200;

	public static bool TryValidate(
		IReadOnlyList<OpeningStockLineInput>? input, DateOnly effectiveDate, DateOnly todayUtc,
		out IReadOnlyList<OpeningStockLine> lines, out string? error)
	{
		lines = [];
		if (input is null || input.Count == 0 || input.Count > MaxLines) { error = "1-200 lines required."; return false; }
		if (input.Select(l => l.ProductId).Distinct().Count() != input.Count) { error = "Duplicate product lines."; return false; }
		if (effectiveDate > todayUtc.AddDays(1)) { error = "Effective date cannot be in the future."; return false; }

		var result = new List<OpeningStockLine>(input.Count);
		foreach (var l in input)
		{
			if (!Quantity.TryCreate(l.Quantity, out var q, out error)) return false;
			if (!UnitCost.TryCreate(l.UnitCost, out var c, out error)) return false;
			var line = new OpeningStockLine(l.ProductId, q, c);
			if (line.Value.Amount < 0.01m) { error = "Line value rounds to zero."; return false; }
			result.Add(line);
		}
		lines = result;
		error = null;
		return true;
	}

	public static Money Total(IEnumerable<OpeningStockLine> lines) =>
		lines.Aggregate(Money.Zero, (sum, l) => sum + l.Value);
}
