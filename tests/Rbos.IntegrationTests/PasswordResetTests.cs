using System.Net;

namespace Rbos.IntegrationTests;

public class PasswordResetTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Password_reset_requires_email_code_revokes_sessions_and_is_single_use()
    {
        var client = new Client(factory);
        await client.Register();

        var login = await client.Send(HttpMethod.Post, "/auth/login", new { email = client.Email, password = Client.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var request = await client.Send(HttpMethod.Post, "/auth/password-reset/request", new { email = client.Email });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var code = factory.EmailSender.CodeFor(client.Email);
        Assert.NotNull(code);

        // A valid, unknown address receives the same outward response as a registered address.
        var unknown = await new Client(factory).Send(
            HttpMethod.Post,
            "/auth/password-reset/request",
            new { email = $"unknown-{Guid.NewGuid():N}@example.test" });
        Assert.Equal(request.StatusCode, unknown.StatusCode);
        Assert.Equal(await request.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());

        var wrongCode = code == "000000" ? "000001" : "000000";
        var rejected = await client.Send(HttpMethod.Post, "/auth/password-reset/confirm", new
        {
            email = client.Email,
            code = wrongCode,
            newPassword = "NewPassword#456",
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var newPassword = "NewPassword#456";
        var reset = await client.Send(HttpMethod.Post, "/auth/password-reset/confirm", new
        {
            email = client.Email,
            code,
            newPassword,
        });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // A password reset revokes sessions immediately; it does not silently sign the user in.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.Raw("/me")).StatusCode);

        var oldPasswordLogin = await client.Send(HttpMethod.Post, "/auth/login", new
        {
            email = client.Email,
            password = Client.Password,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await client.Send(HttpMethod.Post, "/auth/login", new
        {
            email = client.Email,
            password = newPassword,
        });
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);

        var replay = await client.Send(HttpMethod.Post, "/auth/password-reset/confirm", new
        {
            email = client.Email,
            code,
            newPassword = "AnotherPassword#789",
        });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }
}
