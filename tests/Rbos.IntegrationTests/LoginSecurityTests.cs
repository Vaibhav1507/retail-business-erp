using System.Net;

namespace Rbos.IntegrationTests;

public class LoginSecurityTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact] public async Task Account_locks_after_repeated_wrong_passwords_even_with_the_right_one()
    {
        var c = new Client(f); await c.Register();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Post, "/auth/login", new { email = c.Email, password = "Wrong#Password1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Send(HttpMethod.Post, "/auth/login", new { email = c.Email, password = Client.Password })).StatusCode);
    }

    [Fact] public async Task Unknown_email_and_wrong_password_give_the_same_response()
    {
        var c = new Client(f); await c.Register();
        var unknown = await c.Send(HttpMethod.Post, "/auth/login", new { email = "nobody-" + Guid.NewGuid().ToString("N") + "@example.test", password = "Wrong#Password1" });
        var wrong = await c.Send(HttpMethod.Post, "/auth/login", new { email = c.Email, password = "Wrong#Password1" });
        Assert.Equal(unknown.StatusCode, wrong.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await wrong.Content.ReadAsStringAsync());
    }
}
