using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class CatalogueTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static async Task<Client> NewClient(ApiFactory f)
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        return c;
    }

    static object Product(string name, string cat = "Cables", string? brand = null, string? model = null, bool generic = false, object? attrs = null)
        => new { name, categoryName = cat, isGeneric = generic, baseUnit = "pcs", brand, model, attributes = attrs };

    [Fact]
    public async Task Skus_are_sequential_per_business()
    {
        var c = await NewClient(f);
        var a = await (await c.Send(HttpMethod.Post, "/products", Product("A", generic: true))).Content.ReadFromJsonAsync<JsonElement>();
        var b = await (await c.Send(HttpMethod.Post, "/products", Product("B", generic: true))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INT-000001", a.GetProperty("sku").GetString());
        Assert.Equal("INT-000002", b.GetProperty("sku").GetString());
    }

    [Fact]
    public async Task Duplicate_name_brand_model_is_rejected_ignoring_case_and_punctuation()
    {
        var c = await NewClient(f);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/products", Product("USB-C Cable", brand: "Acme", model: "X-1"))).StatusCode);
        var dup = await c.Send(HttpMethod.Post, "/products", Product("usb c cable", brand: "ACME", model: "x1"));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/products", Product("USB-C Cable", brand: "Other", model: "X-1"))).StatusCode);
    }

    [Fact]
    public async Task Product_can_be_edited_and_attributes_are_stored()
    {
        var c = await NewClient(f);
        var id = (await (await c.Send(HttpMethod.Post, "/products", Product("Cable", generic: true))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var put = await c.Send(HttpMethod.Put, $"/products/{id}", Product("Cable 2m", "Chargers", attrs: new { colour = "Black", capacity = "2m" }));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var list = await c.GetJson("/products");
        var p = list.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal("Cable 2m", p.GetProperty("name").GetString());
        Assert.Equal("Chargers", p.GetProperty("category").GetString());
        Assert.Equal("Black", p.GetProperty("attributes").GetProperty("colour").GetString());
    }

    [Fact]
    public async Task Edit_cannot_create_a_duplicate_and_generic_rule_holds()
    {
        var c = await NewClient(f);
        await c.Send(HttpMethod.Post, "/products", Product("One", generic: true));
        var id = (await (await c.Send(HttpMethod.Post, "/products", Product("Two", generic: true))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Put, $"/products/{id}", Product("one", generic: true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, $"/products/{id}", Product("Two", brand: "Acme", generic: true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, $"/products/{id}", Product("Two", generic: true, attrs: new { weight = "1" }))).StatusCode);
    }

    [Fact]
    public async Task Search_matches_name_brand_model_sku_and_attributes_and_filters_inactive()
    {
        var c = await NewClient(f);
        await c.Send(HttpMethod.Post, "/products", Product("Fast charger", "Chargers", "Volt", "VC-20", attrs: new { colour = "Purple" }));
        var gone = (await (await c.Send(HttpMethod.Post, "/products", Product("Old plug", generic: true))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        foreach (var term in new[] { "charger", "volt", "vc-20", "purple", "INT-000001" })
            Assert.Single((await c.GetJson($"/products?q={Uri.EscapeDataString(term)}")).EnumerateArray());
        Assert.Empty((await c.GetJson("/products?q=%25")).EnumerateArray());

        await c.Send(HttpMethod.Put, $"/products/{gone}", new { name = "Old plug", categoryName = "Cables", isGeneric = true, baseUnit = "pcs", isActive = false });
        Assert.Single((await c.GetJson("/products")).EnumerateArray());
        Assert.Equal(2, (await c.GetJson("/products?includeInactive=true")).GetArrayLength());
    }

    [Fact]
    public async Task Category_names_are_case_insensitive_and_renamable()
    {
        var c = await NewClient(f);
        await c.Send(HttpMethod.Post, "/products", Product("A", "Cables", generic: true));
        await c.Send(HttpMethod.Post, "/products", Product("B", "cables ", generic: true));
        var cats = await c.GetJson("/categories");
        Assert.Single(cats.EnumerateArray());
        Assert.Equal(2, cats[0].GetProperty("productCount").GetInt32());
        await c.Send(HttpMethod.Post, "/products", Product("C", "Phones", generic: true));
        var id = cats.EnumerateArray().Single().GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Put, $"/categories/{id}", new { name = "phones" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, $"/categories/{id}", new { name = "Leads" })).StatusCode);
    }

    [Fact]
    public async Task Another_business_cannot_see_or_edit_products()
    {
        var a = await NewClient(f);
        var b = await NewClient(f);
        var id = (await (await a.Send(HttpMethod.Post, "/products", Product("Secret", generic: true))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Empty((await b.GetJson("/products?q=Secret")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await b.Send(HttpMethod.Put, $"/products/{id}", Product("Hacked", generic: true))).StatusCode);
    }
}

public class ProductImportTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static object Row(string name, string cat = "Cables", string unit = "pcs", string? brand = null, bool generic = true) => new { name, categoryName = cat, isGeneric = generic, baseUnit = unit, brand };

    static async Task<Client> NewClient(ApiFactory f)
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        return c;
    }

    [Fact]
    public async Task Preview_classifies_rows_and_writes_nothing()
    {
        var c = await NewClient(f);
        await c.Send(HttpMethod.Post, "/products", new { name = "Existing", categoryName = "Cables", isGeneric = true, baseUnit = "pcs" });
        var rows = new[] { Row("New one", "Chargers"), Row("existing"), Row("New one", "Chargers"), Row("Bad unit", unit: "kg"), Row("Generic w brand", brand: "Acme") };
        var r = await c.Send(HttpMethod.Post, "/products/import/preview", new { rows });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var j = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, j.GetProperty("ok").GetInt32());
        Assert.Equal(2, j.GetProperty("duplicate").GetInt32());
        Assert.Equal(2, j.GetProperty("invalid").GetInt32());
        Assert.Equal("Chargers", j.GetProperty("newCategories")[0].GetString());
        Assert.Single((await c.GetJson("/products")).EnumerateArray());
    }

    [Fact]
    public async Task Commit_creates_valid_rows_skips_others_and_replays_by_key()
    {
        var c = await NewClient(f);
        var rows = new[] { Row("Item A"), Row("Item B", "Chargers"), Row("Item A"), Row("Bad", unit: "kg") };
        var key = Guid.NewGuid().ToString();
        var r1 = await (await c.Send(HttpMethod.Post, "/products/import/commit", new { rows }, key)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, r1.GetProperty("created").GetInt32());
        Assert.Equal(2, r1.GetProperty("skipped").GetInt32());
        Assert.False(r1.GetProperty("replayed").GetBoolean());
        var r2 = await (await c.Send(HttpMethod.Post, "/products/import/commit", new { rows }, key)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(r2.GetProperty("replayed").GetBoolean());
        var list = await c.GetJson("/products");
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(new[] { "INT-000001", "INT-000002" }, list.EnumerateArray().Select(x => x.GetProperty("sku").GetString()!).Order().ToArray());
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/products/import/commit", new { rows = new[] { Row("Different") } }, key)).StatusCode);
    }

    [Fact]
    public async Task Commit_requires_key_and_limits_rows()
    {
        var c = await NewClient(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/products/import/commit", new { rows = new[] { Row("A") } })).StatusCode);
        var many = Enumerable.Range(0, 201).Select(i => Row("P" + i)).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/products/import/preview", new { rows = many })).StatusCode);
    }
}


public class CategoryAttributeTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static async Task<Client> NewClient(ApiFactory f)
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        return c;
    }

    [Fact]
    public async Task Category_attributes_are_tenant_scoped_and_configurable()
    {
        var owner = await NewClient(f);
        await owner.Send(HttpMethod.Post, "/products", new { name = "Washer", categoryName = "Washing Machines", isGeneric = true, baseUnit = "pcs" });
        var category = (await owner.GetJson("/categories")).EnumerateArray().Single();
        var id = category.GetProperty("id").GetGuid();
        var definitions = new[] { new { key = "capacity_kg", label = "Capacity", dataType = "decimal", unit = "kg", isRequired = true, isSearchable = false, isFilterable = true, displayOrder = 1, options = Array.Empty<string>() } };
        Assert.Equal(HttpStatusCode.OK, (await owner.Send(HttpMethod.Put, $"/categories/{id}/attributes", definitions)).StatusCode);
        var saved = await owner.GetJson($"/categories/{id}/attributes");
        Assert.Equal("capacity_kg", saved[0].GetProperty("key").GetString());

        var other = await NewClient(f);
        Assert.Empty((await other.GetJson($"/categories/{id}/attributes")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await other.Send(HttpMethod.Put, $"/categories/{id}/attributes", definitions)).StatusCode);
    }

    [Fact]
    public async Task Required_category_attributes_are_validated_during_import_preview()
    {
        var owner = await NewClient(f);
        await owner.Send(HttpMethod.Post, "/products", new { name = "Washer", categoryName = "Washing Machines", isGeneric = true, baseUnit = "pcs" });
        var id = (await owner.GetJson("/categories")).EnumerateArray().Single().GetProperty("id").GetGuid();
        var definitions = new[] { new { key = "capacity_kg", label = "Capacity", dataType = "decimal", unit = "kg", isRequired = true, isSearchable = false, isFilterable = false, displayOrder = 0, options = Array.Empty<string>() } };
        await owner.Send(HttpMethod.Put, $"/categories/{id}/attributes", definitions);
        object[] rows =
        {
            new { name = "Washer Model A", categoryName = "Washing Machines", isGeneric = true, baseUnit = "pcs", attributes = new { }, specs = new Dictionary<string, string>() },
            new { name = "Washer Model B", categoryName = "Washing Machines", isGeneric = true, baseUnit = "pcs", attributes = new { }, specs = new Dictionary<string, string> { ["capacity_kg"] = "8" } }
        };
        var preview = await (await owner.Send(HttpMethod.Post, "/products/import/preview", new { rows })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, preview.GetProperty("invalid").GetInt32());
        Assert.Equal(1, preview.GetProperty("ok").GetInt32());
        Assert.Contains("Capacity", preview.GetProperty("rows")[0].GetProperty("message").GetString());
    }
    [Fact]
    public async Task Imported_dynamic_specs_are_persisted_and_searchable()
    {
        var owner = await NewClient(f);
        object[] rows =
        {
            new { name = "LG Washer 8kg", categoryName = "Washing Machines", isGeneric = false, baseUnit = "pcs", brand = "LG", model = "TX-8", attributes = new { colour = "Silver" }, specs = new Dictionary<string, string> { ["capacity_kg"] = "8", ["load_type"] = "Top Load" } }
        };
        var key = Guid.NewGuid().ToString();
        var committed = await (await owner.Send(HttpMethod.Post, "/products/import/commit", new { rows }, key)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, committed.GetProperty("created").GetInt32());
        var product = (await owner.GetJson("/products?q=Top%20Load")).EnumerateArray().Single();
        Assert.Equal("8", product.GetProperty("specs").GetProperty("capacity_kg").GetString());
        Assert.Equal("Silver", product.GetProperty("attributes").GetProperty("colour").GetString());
    }

}
