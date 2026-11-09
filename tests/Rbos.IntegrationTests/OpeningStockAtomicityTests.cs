using System.Net;

namespace Rbos.IntegrationTests;

public class OpeningStockAtomicityTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Rejected_opening_stock_with_a_bad_line_leaves_stock_and_ledger_unchanged()
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        var good = await c.AddProduct("Good item");
        var loc = await c.Location();
        var tb = (await c.GetJson("/reports/trial-balance")).ToString();
        var stock = (await c.GetJson("/stock")).ToString();

        var body = new
        {
            locationId = loc,
            effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            lines = new[] { new { productId = good, quantity = 5m, unitCost = 10m }, new { productId = Guid.NewGuid(), quantity = 5m, unitCost = 10m } }
        };
        var r = await c.Send(HttpMethod.Post, "/opening-stock", body, Guid.NewGuid().ToString());

        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(tb, (await c.GetJson("/reports/trial-balance")).ToString());
        Assert.Equal(stock, (await c.GetJson("/stock")).ToString());
    }
}
