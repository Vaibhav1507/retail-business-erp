using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class CashClosingTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();

    [Fact] public async Task Cash_closing_records_difference_and_rejects_duplicate_day()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var m = (await c.GetJson("/money-accounts"))[0].GetProperty("id").GetGuid();
        await c.Send(HttpMethod.Post, "/owner-transactions", new { kind = "Capital", date = Today, moneyAccountId = m, amount = 1000m }, K());

        var preview = await c.GetJson("/cash-closings/preview?moneyAccountId=" + m + "&date=" + Today);
        Assert.Equal(1000m, preview.GetProperty("expected").GetDecimal());

        var ok = await c.Send(HttpMethod.Post, "/cash-closings", new { moneyAccountId = m, date = Today, counted = 990m, note = "short" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(-10m, (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("difference").GetDecimal());

        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/cash-closings", new { moneyAccountId = m, date = Today, counted = 1000m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/cash-closings", new { moneyAccountId = m, date = "2000-01-01", counted = -1m })).StatusCode);
        Assert.Equal(1, (await c.GetJson("/cash-closings")).GetArrayLength());
    }
}
