namespace Rbos.Domain;

/// <summary>A1: GST split for a tax-inclusive amount. Intra-state supply = CGST + SGST (half each); inter-state = IGST.</summary>
public readonly record struct GstBreakdown(decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst)
{
    public decimal Tax => Cgst + Sgst + Igst;
    public decimal Total => Taxable + Tax;
}

public static class GstCalculator
{
    public static readonly decimal[] StandardRates = [0m, 0.25m, 3m, 5m, 12m, 18m, 28m];

    public static bool IsInterState(string? sellerState, string? placeOfSupplyState) =>
        !string.IsNullOrWhiteSpace(sellerState) && !string.IsNullOrWhiteSpace(placeOfSupplyState)
        && !string.Equals(sellerState.Trim(), placeOfSupplyState.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Splits a GST-inclusive amount. Rounds tax to paise; CGST gets the odd paisa so CGST+SGST always equals total tax.</summary>
    public static GstBreakdown FromInclusive(decimal inclusiveAmount, decimal ratePercent, bool interState)
    {
        if (ratePercent < 0 || ratePercent > 100) throw new ArgumentOutOfRangeException(nameof(ratePercent));
        var taxable = Math.Round(inclusiveAmount * 100m / (100m + ratePercent), 2, MidpointRounding.AwayFromZero);
        var tax = inclusiveAmount - taxable;
        if (interState) return new GstBreakdown(taxable, 0m, 0m, tax);
        var sgst = Math.Round(tax / 2m, 2, MidpointRounding.ToZero);
        return new GstBreakdown(taxable, tax - sgst, sgst, 0m);
    }

    public static bool IsValidHsn(string? hsn) =>
        hsn is { Length: 4 or 6 or 8 } && hsn.All(char.IsAsciiDigit);
}
