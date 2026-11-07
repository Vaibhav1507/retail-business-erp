namespace Rbos.Domain;

/// <summary>Monetary amount in INR, always held as decimal and rounded to 2 places (half away from zero).</summary>
public readonly record struct Money
{
	public decimal Amount { get; }
	Money(decimal amount) => Amount = amount;

	public static Money Zero => new(0m);
	public static Money Of(decimal amount) => new(Round(amount));
	public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
	public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
}

/// <summary>Strictly positive quantity, at most 3 decimal places and 1,000,000.</summary>
public readonly record struct Quantity
{
	public const decimal Max = 1_000_000m;
	public decimal Value { get; }
	Quantity(decimal value) => Value = value;

	public static bool TryCreate(decimal value, out Quantity quantity, out string? error)
	{
		quantity = default;
		error = null;
		if (value <= 0) error = "Quantity must be greater than zero.";
		else if (value > Max) error = "Quantity is too large.";
		else if (decimal.Round(value, 3) != value) error = "Quantity allows at most 3 decimal places.";
		else quantity = new Quantity(value);
		return error is null;
	}
}

/// <summary>Strictly positive unit cost in INR, at most 4 decimal places and 10,000,000.</summary>
public readonly record struct UnitCost
{
	public const decimal Max = 10_000_000m;
	public decimal Value { get; }
	UnitCost(decimal value) => Value = value;

	public static bool TryCreate(decimal value, out UnitCost cost, out string? error)
	{
		cost = default;
		error = null;
		if (value <= 0) error = "Unit cost must be greater than zero (unknown cost is not allowed).";
		else if (value > Max) error = "Unit cost is too large.";
		else if (decimal.Round(value, 4) != value) error = "Unit cost allows at most 4 decimal places.";
		else cost = new UnitCost(value);
		return error is null;
	}
}
