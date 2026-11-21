using Rbos.Domain;

namespace Rbos.UnitTests;

public sealed class SaleLineRulesTests
{
    static readonly DateOnly Today = new(2026, 11, 18);
    static readonly Guid Product = Guid.NewGuid();

    static bool Validate(SaleLineInput line, out IReadOnlyList<SaleLine> lines, out string? error) =>
        SalesRules.TryValidateSale(
            [line], "Cash", null, null, false, Today, Today,
            out lines, out _, out _, out _, out error);

    [Fact]
    public void Serial_number_characters_are_preserved_exactly()
    {
        const string serial = "abC-01oI";
        Assert.True(Validate(new SaleLineInput(Product, 1m, 100m, 0m, [serial]), out var lines, out var error), error);
        Assert.Equal(serial, Assert.Single(Assert.Single(lines).Serials!));
    }

    [Fact]
    public void Dual_imei_identifiers_are_validated_and_kept_separate_from_serial()
    {
        const string serial = "SERIAL12345";
        const string imei1 = "490154203237518";
        const string imei2 = "490154203237526";
        Assert.True(Validate(new SaleLineInput(Product, 1m, 100m, 0m, [serial], null, null, [imei1], [imei2]),
            out var lines, out var error), error);
        var line = Assert.Single(lines);
        Assert.Equal(serial, Assert.Single(line.Serials!));
        Assert.Equal(imei1, Assert.Single(line.Imei1s!));
        Assert.Equal(imei2, Assert.Single(line.Imei2s!));
    }

    [Fact]
    public void Invalid_imei_check_digit_is_rejected()
    {
        Assert.False(Validate(new SaleLineInput(Product, 1m, 100m, 0m, null, null, null, ["490154203237519"]),
            out _, out _));
    }
}
