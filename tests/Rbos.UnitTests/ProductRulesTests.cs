using Rbos.Domain;

namespace Rbos.UnitTests;

public class ProductRulesTests
{
    static ProductInput Ok(Action<Dictionary<string, string>>? attrs = null, bool generic = false, string? brand = "Acme", string? model = "X1")
    {
        var a = new Dictionary<string, string>();
        attrs?.Invoke(a);
        return new ProductInput("USB Cable", "Cables", generic, "pcs", brand, model, a);
    }

    [Fact]
    public void Valid_product_has_no_error() => Assert.Null(ProductRules.Validate(Ok()));

    [Fact]
    public void Generic_product_cannot_carry_brand_or_model()
        => Assert.NotNull(ProductRules.Validate(Ok(generic: true)));

    [Fact]
    public void Generic_product_without_brand_or_model_is_valid()
        => Assert.Null(ProductRules.Validate(Ok(generic: true, brand: null, model: null)));

    [Fact]
    public void Unknown_attribute_key_is_rejected()
        => Assert.NotNull(ProductRules.Validate(Ok(a => a["weight"] = "1kg")));

    [Fact]
    public void Known_attribute_keys_are_accepted()
        => Assert.Null(ProductRules.Validate(Ok(a => { a["colour"] = "Black"; a["capacity"] = "1m"; })));

    [Fact]
    public void Unknown_unit_is_rejected()
        => Assert.NotNull(ProductRules.Validate(Ok() with { BaseUnit = "kg" }));

    [Fact]
    public void Duplicate_key_ignores_case_spaces_and_punctuation()
        => Assert.Equal(ProductRules.DuplicateKey("USB-C  Cable", "Acme", "X-1"), ProductRules.DuplicateKey("usb c cable", " ACME ", "x1"));

    [Fact]
    public void Duplicate_key_distinguishes_brand_and_model()
    {
        Assert.NotEqual(ProductRules.DuplicateKey("Cable", "A", "1"), ProductRules.DuplicateKey("Cable", "B", "1"));
        Assert.NotEqual(ProductRules.DuplicateKey("Cable", null, null), ProductRules.DuplicateKey("Cable", "A", null));
    }

    [Fact]
    public void Manufacturer_model_identity_ignores_catalogue_display_name_and_category()
    {
        Assert.Equal(
            ProductRules.IdentityKey("French Door Refrigerator GV-B25FFGMB", "LG", "GV-B25FFGMB"),
            ProductRules.IdentityKey("LG French Door refrigerator", "LG", "GV-B25FFGMB"));
    }

    [Fact]
    public void Manufacturer_model_identity_keeps_distinct_model_variants_separate()
    {
        Assert.NotEqual(
            ProductRules.IdentityKey("French Door Refrigerator", "LG", "GV-B25FFGMB"),
            ProductRules.IdentityKey("French Door Refrigerator", "LG", "GV-B25FFLMB"));
    }

    [Fact]
    public void Placeholder_model_values_do_not_collapse_unrelated_products()
    {
        Assert.NotEqual(
            ProductRules.IdentityKey("Air Conditioner", "LG", "N/A"),
            ProductRules.IdentityKey("Washing Machine", "LG", "N/A"));
    }

    [Fact]
    public void No_model_placeholder_uses_name_fallback()
    {
        Assert.NotEqual(
            ProductRules.IdentityKey("Air Conditioner", "LG", "No Model"),
            ProductRules.IdentityKey("Washing Machine", "LG", "No Model"));
    }

    [Fact]
    public void Shared_master_identity_reuses_model_across_categories_but_scopes_generic_goods()
    {
        Assert.Equal(
            ProductRules.MasterIdentityKey("French Door", "Refrigerators", "LG", "GV-B25FFGMB"),
            ProductRules.MasterIdentityKey("LG refrigerator", "Home Appliances", "LG", "GV-B25FFGMB"));
        Assert.NotEqual(
            ProductRules.MasterIdentityKey("Cable", "Cables", null, null),
            ProductRules.MasterIdentityKey("Cable", "Chargers", null, null));
    }

    [Theory]
    [InlineData(1, "INT-000001")]
    [InlineData(42, "INT-000042")]
    [InlineData(1234567, "INT-1234567")]
    public void Sku_is_zero_padded(long n, string expected) => Assert.Equal(expected, ProductRules.FormatSku(n));

    [Fact]
    public void Clean_attributes_drops_blank_values()
    {
        var r = ProductRules.CleanAttributes(new Dictionary<string, string> { ["colour"] = " Red ", ["capacity"] = "  " });
        Assert.Single(r);
        Assert.Equal("Red", r["colour"]);
    }
}
