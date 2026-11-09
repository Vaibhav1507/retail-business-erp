using Rbos.Domain;

namespace Rbos.UnitTests;

public class PurchaseGstTests
{
    static readonly DateOnly Today = new(2026, 5, 1);

    static IReadOnlyList<PurchaseLine> Lines(params PurchaseLineInput[] input)
    {
        Assert.True(PurchaseRules.TryValidateReceipt(input, Today, Today, out var lines, out var err), err);
        return lines;
    }

    static decimal Sum(IEnumerable<JournalLineDraft> j, string account, bool debit)
        => j.Where(x => x.AccountCode == account).Sum(x => debit ? x.Debit : x.Credit);

    [Fact]
    public void Totals_split_taxable_gst_and_grand_total()
    {
        var lines = Lines(new PurchaseLineInput(Guid.NewGuid(), 10m, 100m, 180m), new PurchaseLineInput(Guid.NewGuid(), 2m, 50m, 18m));
        Assert.Equal(1100m, PurchaseRules.Total(lines).Amount);
        Assert.Equal(198m, PurchaseRules.GstTotal(lines).Amount);
        Assert.Equal(1298m, PurchaseRules.GrandTotal(lines).Amount);
    }

    [Fact]
    public void Gst_defaults_to_zero_so_old_payloads_still_work()
    {
        var lines = Lines(new PurchaseLineInput(Guid.NewGuid(), 3m, 10m));
        Assert.Equal(0m, PurchaseRules.GstTotal(lines).Amount);
        Assert.Equal(30m, PurchaseRules.GrandTotal(lines).Amount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(1000.01)]
    public void Invalid_gst_is_rejected(double gst)
    {
        var input = new[] { new PurchaseLineInput(Guid.NewGuid(), 10m, 100m, (decimal)gst) };
        Assert.False(PurchaseRules.TryValidateReceipt(input, Today, Today, out _, out var err));
        Assert.False(string.IsNullOrWhiteSpace(err));
    }

    [Theory]
    [InlineData("Regular", true)]
    [InlineData("Composition", false)]
    [InlineData("NotRegistered", false)]
    [InlineData("Undecided", false)]
    [InlineData(null, false)]
    public void Only_regular_businesses_claim_input_gst(string? status, bool expected)
        => Assert.Equal(expected, PurchaseRules.CanClaimGst(status));

    [Fact]
    public void Composition_capitalises_gst_into_unit_cost()
    {
        var lines = Lines(new PurchaseLineInput(Guid.NewGuid(), 10m, 100m, 180m));
        Assert.Equal(118m, lines[0].LandedUnitCost(false));
        Assert.Equal(1180m, lines[0].StockValue(false).Amount);
        var j = PurchaseRules.ReceiptJournal(lines, false);
        Assert.Equal(1180m, Sum(j, BalancedJournalPolicy.InventoryAccount, true));
        Assert.Equal(0m, Sum(j, PurchaseRules.GstInputAccount, true));
        Assert.Equal(1180m, Sum(j, "2000", false));
    }

    [Fact]
    public void Regular_claims_gst_separately_and_keeps_inventory_net()
    {
        var lines = Lines(new PurchaseLineInput(Guid.NewGuid(), 10m, 100m, 180m));
        Assert.Equal(100m, lines[0].LandedUnitCost(true));
        Assert.Equal(1000m, lines[0].StockValue(true).Amount);
        var j = PurchaseRules.ReceiptJournal(lines, true);
        Assert.Equal(1000m, Sum(j, BalancedJournalPolicy.InventoryAccount, true));
        Assert.Equal(180m, Sum(j, PurchaseRules.GstInputAccount, true));
        Assert.Equal(1180m, Sum(j, "2000", false));
        Assert.Equal(j.Sum(x => x.Debit), j.Sum(x => x.Credit));
    }

    [Fact]
    public void Supplier_return_reverses_gst_input_when_it_was_claimed()
    {
        var j = AfterSalesRules.SupplierReturnJournal(Money.Of(118m), Money.Of(100m), Money.Of(18m));
        Assert.Equal(118m, Sum(j, "2000", true));
        Assert.Equal(100m, Sum(j, BalancedJournalPolicy.InventoryAccount, false));
        Assert.Equal(18m, Sum(j, PurchaseRules.GstInputAccount, false));
        Assert.Equal(j.Sum(x => x.Debit), j.Sum(x => x.Credit));
    }

    [Fact]
    public void Supplier_return_without_claimed_gst_is_unchanged()
    {
        var j = AfterSalesRules.SupplierReturnJournal(Money.Of(118m), Money.Of(118m));
        Assert.Equal(0m, Sum(j, PurchaseRules.GstInputAccount, false));
        Assert.Equal(j.Sum(x => x.Debit), j.Sum(x => x.Credit));
    }
}
