using Rbos.Domain;

namespace Rbos.UnitTests;

public class MoneyTests
{
	[Theory]
	[InlineData("10.004", "10.00")]
	[InlineData("10.005", "10.01")]
	[InlineData("10.015", "10.02")]
	[InlineData("0.995", "1.00")]
	[InlineData("255.000", "255.00")]
	public void Rounds_to_two_places_half_away_from_zero(string input, string expected)
		=> Assert.Equal(decimal.Parse(expected), Money.Of(decimal.Parse(input)).Amount);

	[Fact]
	public void Addition_is_exact_decimal()
		=> Assert.Equal(0.30m, (Money.Of(0.10m) + Money.Of(0.20m)).Amount);
}

public class QuantityAndCostTests
{
	[Theory]
	[InlineData("0.001")]
	[InlineData("1")]
	[InlineData("999999.999")]
	[InlineData("1000000")]
	public void Valid_quantities_are_accepted(string v)
		=> Assert.True(Quantity.TryCreate(decimal.Parse(v), out _, out var err), err);

	[Theory]
	[InlineData("0")]
	[InlineData("-1")]
	[InlineData("-0.001")]
	[InlineData("0.0001")]
	[InlineData("1.0001")]
	[InlineData("1000000.001")]
	public void Invalid_quantities_are_rejected(string v)
		=> Assert.False(Quantity.TryCreate(decimal.Parse(v), out _, out var err) || err is null);

	[Theory]
	[InlineData("0.0001")]
	[InlineData("25.5")]
	[InlineData("10000000")]
	public void Valid_unit_costs_are_accepted(string v)
		=> Assert.True(UnitCost.TryCreate(decimal.Parse(v), out _, out var err), err);

	[Theory]
	[InlineData("0")]
	[InlineData("-5")]
	[InlineData("0.00001")]
	[InlineData("10000000.0001")]
	public void Invalid_unit_costs_are_rejected(string v)
		=> Assert.False(UnitCost.TryCreate(decimal.Parse(v), out _, out _));

	[Fact]
	public void Zero_cost_message_says_unknown_cost_not_allowed()
	{
		UnitCost.TryCreate(0m, out _, out var err);
		Assert.Contains("unknown cost", err);
	}
}

public class OpeningStockPolicyTests
{
	static readonly DateOnly Today = new(2026, 11, 1);
	static readonly Guid P1 = Guid.NewGuid(), P2 = Guid.NewGuid();

	[Fact]
	public void Valid_command_computes_line_values_and_total()
	{
		var ok = OpeningStockPolicy.TryValidate([new(P1, 10m, 25.5m), new(P2, 3m, 33.3333m)], Today, Today, out var lines, out var err);
		Assert.True(ok, err);
		Assert.Equal(255.00m, lines[0].Value.Amount);
		Assert.Equal(100.00m, lines[1].Value.Amount);
		Assert.Equal(355.00m, OpeningStockPolicy.Total(lines).Amount);
	}

	[Fact]
	public void Empty_or_null_lines_are_rejected()
	{
		Assert.False(OpeningStockPolicy.TryValidate([], Today, Today, out _, out _));
		Assert.False(OpeningStockPolicy.TryValidate(null, Today, Today, out _, out _));
	}

	[Fact]
	public void More_than_200_lines_are_rejected()
	{
		var many = Enumerable.Range(0, 201).Select(_ => new OpeningStockLineInput(Guid.NewGuid(), 1m, 1m)).ToList();
		Assert.False(OpeningStockPolicy.TryValidate(many, Today, Today, out _, out _));
	}

	[Fact]
	public void Exactly_200_lines_are_accepted()
	{
		var many = Enumerable.Range(0, 200).Select(_ => new OpeningStockLineInput(Guid.NewGuid(), 1m, 1m)).ToList();
		Assert.True(OpeningStockPolicy.TryValidate(many, Today, Today, out _, out var err), err);
	}

	[Fact]
	public void Duplicate_products_are_rejected()
		=> Assert.False(OpeningStockPolicy.TryValidate([new(P1, 1m, 1m), new(P1, 2m, 2m)], Today, Today, out _, out _));

	[Theory]
	[InlineData(0, 5)]
	[InlineData(-1, 5)]
	[InlineData(1, 0)]
	[InlineData(1, -5)]
	public void Zero_or_negative_quantity_or_cost_is_rejected(int q, int c)
		=> Assert.False(OpeningStockPolicy.TryValidate([new(P1, q, c)], Today, Today, out _, out _));

	[Fact]
	public void Line_value_that_rounds_to_zero_is_rejected()
		=> Assert.False(OpeningStockPolicy.TryValidate([new(P1, 0.001m, 0.0001m)], Today, Today, out _, out var err) || err is null);

	[Fact]
	public void Future_date_beyond_one_day_is_rejected_but_tomorrow_allowed()
	{
		Assert.True(OpeningStockPolicy.TryValidate([new(P1, 1m, 1m)], Today.AddDays(1), Today, out _, out _));
		Assert.False(OpeningStockPolicy.TryValidate([new(P1, 1m, 1m)], Today.AddDays(2), Today, out _, out _));
	}
}

public class WeightedAverageCostTests
{
	static CostState Receive(CostState s, decimal q, decimal c)
	{
		Assert.True(Quantity.TryCreate(q, out var qty, out _));
		Assert.True(UnitCost.TryCreate(c, out var cost, out _));
		return WeightedAverageCostCalculator.ApplyReceipt(s, qty, cost);
	}

	[Fact]
	public void First_receipt_sets_wac_to_unit_cost()
	{
		var s = Receive(CostState.Empty, 10m, 25.5m);
		Assert.Equal(10m, s.TotalQuantity);
		Assert.Equal(255.00m, s.InventoryValue);
		Assert.Equal(25.5m, s.Wac);
	}

	[Fact]
	public void Second_receipt_at_different_cost_blends_wac()
	{
		var s = Receive(Receive(CostState.Empty, 10m, 20m), 10m, 30m);
		Assert.Equal(20m, s.TotalQuantity);
		Assert.Equal(500.00m, s.InventoryValue);
		Assert.Equal(25m, s.Wac);
	}

	[Fact]
	public void Unequal_quantities_weight_the_average()
	{
		var s = Receive(Receive(CostState.Empty, 3m, 10m), 1m, 20m);
		Assert.Equal(4m, s.TotalQuantity);
		Assert.Equal(50.00m, s.InventoryValue);
		Assert.Equal(12.5m, s.Wac);
	}

	[Fact]
	public void Wac_rounds_to_four_places_and_value_stays_two_places()
	{
		var s = Receive(Receive(CostState.Empty, 1m, 10m), 2m, 10m);
		Assert.Equal(30.00m, s.InventoryValue);
		var t = Receive(CostState.Empty, 3m, 33.3333m);
		Assert.Equal(100.00m, t.InventoryValue);
		Assert.Equal(33.3333m, t.Wac);
	}

	[Fact]
	public void Fractional_quantity_is_supported()
	{
		var s = Receive(CostState.Empty, 2.5m, 4m);
		Assert.Equal(10.00m, s.InventoryValue);
		Assert.Equal(4m, s.Wac);
	}
}

public class BalancedJournalTests
{
	[Fact]
	public void Opening_stock_journal_debits_inventory_and_credits_obe_equally()
	{
		var j = BalancedJournalPolicy.OpeningStock(Money.Of(255m));
		Assert.Equal(2, j.Count);
		var inv = j.Single(l => l.AccountCode == "1400");
		var obe = j.Single(l => l.AccountCode == "3900");
		Assert.Equal(255.00m, inv.Debit);
		Assert.Equal(0m, inv.Credit);
		Assert.Equal(255.00m, obe.Credit);
		Assert.Equal(0m, obe.Debit);
		Assert.Equal(j.Sum(l => l.Debit), j.Sum(l => l.Credit));
	}

	[Fact]
	public void Zero_total_is_rejected()
		=> Assert.Throws<ArgumentOutOfRangeException>(() => BalancedJournalPolicy.OpeningStock(Money.Zero));

	[Fact]
	public void Unbalanced_journal_is_rejected()
		=> Assert.Throws<InvalidOperationException>(() => BalancedJournalPolicy.AssertBalanced([new("1400", 10m, 0m), new("3900", 0m, 9.99m)]));

	[Fact]
	public void Line_with_both_sides_is_rejected()
		=> Assert.Throws<InvalidOperationException>(() => BalancedJournalPolicy.AssertBalanced([new("1400", 10m, 10m), new("3900", 0m, 0m)]));

	[Fact]
	public void Single_line_journal_is_rejected()
		=> Assert.Throws<InvalidOperationException>(() => BalancedJournalPolicy.AssertBalanced([new("1400", 10m, 0m)]));
}
