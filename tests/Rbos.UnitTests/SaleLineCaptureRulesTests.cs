using Rbos.Domain;

namespace Rbos.UnitTests;

public sealed class SaleLineCaptureRulesTests
{
    static readonly DateOnly Today = new(2026, 11, 18);
    static readonly Guid Product = Guid.NewGuid();
    static readonly Guid CaptureA = Guid.NewGuid();
    static readonly Guid CaptureB = Guid.NewGuid();

    static bool Validate(SaleLineInput line, out IReadOnlyList<SaleLine> lines, out string? error) =>
        SalesRules.TryValidateSale(
            [line], "Cash", null, null, false, Today, Today,
            out lines, out _, out _, out _, out error);

    [Fact]
    public void Reviewed_capture_references_are_preserved_in_normalised_sale_line()
    {
        Assert.True(Validate(new SaleLineInput(Product, 2m, 100m, 0m, null, [CaptureA, CaptureB]), out var lines, out var error), error);
        Assert.Equal(new[] { CaptureA, CaptureB }, Assert.Single(lines).CaptureIds);
    }

    [Fact]
    public void Duplicate_or_empty_capture_ids_are_rejected()
    {
        Assert.False(Validate(new SaleLineInput(Product, 2m, 100m, 0m, null, [CaptureA, CaptureA]), out _, out _));
        Assert.False(Validate(new SaleLineInput(Product, 1m, 100m, 0m, null, [Guid.Empty]), out _, out _));
    }

    [Fact]
    public void Photo_count_cannot_exceed_whole_unit_quantity()
    {
        Assert.False(Validate(new SaleLineInput(Product, 1m, 100m, 0m, null, [CaptureA, CaptureB]), out _, out _));
        Assert.False(Validate(new SaleLineInput(Product, 1.5m, 100m, 0m, null, [CaptureA]), out _, out _));
    }

    [Fact]
    public void Existing_sale_lines_remain_valid_when_capture_ids_are_omitted()
    {
        Assert.True(Validate(new SaleLineInput(Product, 1m, 100m, 0m), out var lines, out var error), error);
        Assert.Null(Assert.Single(lines).CaptureIds);
    }
}
