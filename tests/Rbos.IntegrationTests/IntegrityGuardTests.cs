using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class IntegrityGuardTests(ApiFactory f) : IClassFixture<ApiFactory>
{
	[Fact]
	public async Task Second_opening_stock_for_same_location_is_rejected()
	{
		var c = new Client(f);
		await c.SignUpAndBootstrap();
		var p = await c.AddProduct("Adapter");
		var loc = await c.Location();
		var body = new { locationId = loc, effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow), lines = new[] { new { productId = p, quantity = 2m, unitCost = 10m } } };
		Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/opening-stock", body, Guid.NewGuid().ToString())).StatusCode);
		Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/opening-stock", body, Guid.NewGuid().ToString())).StatusCode);
		var stock = await c.GetJson("/stock");
		Assert.Equal(2m, stock[0].GetProperty("quantity").GetDecimal());
	}

	[Fact]
	public async Task Second_bootstrap_with_different_key_returns_existing_business()
	{
		var c = new Client(f);
		await c.SignUpAndBootstrap();
		var r = await c.Send(HttpMethod.Post, "/bootstrap", new { businessName = "Another", branchName = "B", locationName = "L" }, Guid.NewGuid().ToString());
		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
		Assert.True((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replayed").GetBoolean());
	}
}
