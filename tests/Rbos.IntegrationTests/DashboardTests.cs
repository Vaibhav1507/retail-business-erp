using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Rbos.IntegrationTests;

public class DashboardTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<Guid> Id(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); }

    [Fact]
    public async Task Dashboard_summarises_sales_dues_and_requires_login()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location(); var p = await c.AddProduct("Cable");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        var cust = await Id(await c.Send(HttpMethod.Post, "/customers", new { name = "Ravi", phone = "9990002222" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 20m, unitCost = 20m } } }, K()));
        await Id(await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 50m, payMethod = "Cash", lines = new[] { new { productId = p, quantity = 4m, unitPrice = 50m, discount = 0m } } }, K()));

        var r = await c.Send(HttpMethod.Get, "/dashboard");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var j = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, j.GetProperty("salesToday").GetProperty("bills").GetInt32());
        Assert.Equal(200m, j.GetProperty("salesToday").GetProperty("total").GetDecimal());
        Assert.Equal(150m, j.GetProperty("moneyToCollect").GetDecimal());
        Assert.Equal(400m, j.GetProperty("moneyToPay").GetDecimal());

        var anon = await f.CreateClient().GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
    }
}
