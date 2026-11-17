using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Rbos.IntegrationTests;

public sealed class PersistentLoginTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string AppConnection()
    {
        var config = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        return config["ConnectionStrings:RbosTestApp"]!;
    }

    private static void AssertPersistentCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values), "Login must issue a Set-Cookie header.");
        var cookie = values!.FirstOrDefault(value => value.StartsWith("rbos.auth=", StringComparison.Ordinal));
        Assert.NotNull(cookie);
        Assert.True(cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.True(cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));

        var expires = cookie.Split(';')
            .Select(part => part.Trim())
            .FirstOrDefault(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(expires);
        Assert.True(DateTimeOffset.TryParse(
            expires["expires=".Length..],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var cookieExpiry));
        Assert.InRange(cookieExpiry, DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));
    }

    private static async Task<(Guid Id, DateTimeOffset AuthenticatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt)> LatestSession(string email)
    {
        await using var connection = new NpgsqlConnection(AppConnection());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select s.id, s.authenticated_at, s.expires_at, s.revoked_at
            from identity.auth_session s
            join identity."AspNetUsers" u on u."Id" = s.user_id
            where u."NormalizedUserName" = @name
            order by s.created_at desc
            limit 1
            """, connection);
        command.Parameters.AddWithValue("name", email.ToUpperInvariant());
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "A successful login must create a server-side session.");
        return (
            reader.GetGuid(0),
            reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3));
    }

    [Fact]
    public async Task Password_login_uses_a_30_day_persistent_cookie_and_90_day_server_limit()
    {
        var client = new Client(factory);
        await client.Register();

        var login = await client.Send(HttpMethod.Post, "/auth/login",
            new { email = client.Email, password = Client.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AssertPersistentCookie(login);

        var session = await LatestSession(client.Email);
        Assert.Null(session.RevokedAt);
        Assert.InRange((session.ExpiresAt - session.AuthenticatedAt).TotalDays, 89.99, 90.01);
        Assert.Equal(HttpStatusCode.OK, (await client.Raw("/me")).StatusCode);
    }

    [Fact]
    public async Task OTP_login_uses_the_same_persistent_session_policy()
    {
        var client = new Client(factory);
        await client.Register();

        string mobile;
        await using (var connection = new NpgsqlConnection(AppConnection()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """select "PhoneNumber" from identity."AspNetUsers" where "NormalizedUserName" = @name""",
                connection);
            command.Parameters.AddWithValue("name", client.Email.ToUpperInvariant());
            mobile = (string)(await command.ExecuteScalarAsync())!;
            await using var agePreviousChallenge = new NpgsqlCommand(
                "update identity.otp_challenge set created_at = now() - interval '2 minutes' where mobile = @mobile",
                connection);
            agePreviousChallenge.Parameters.AddWithValue("mobile", mobile);
            await agePreviousChallenge.ExecuteNonQueryAsync();
        }

        var sent = await client.Send(HttpMethod.Post, "/auth/otp/send", new { mobile, purpose = "login" });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var devCode = (await sent.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("devCode").GetString();
        Assert.False(string.IsNullOrWhiteSpace(devCode));

        var login = await client.Send(HttpMethod.Post, "/auth/login/otp", new { mobile, code = devCode });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AssertPersistentCookie(login);

        var session = await LatestSession(client.Email);
        Assert.InRange((session.ExpiresAt - session.AuthenticatedAt).TotalDays, 89.99, 90.01);
    }

    [Fact]
    public async Task Server_rejects_a_session_after_its_absolute_expiry()
    {
        var client = new Client(factory);
        await client.Register();
        Assert.Equal(HttpStatusCode.OK,
            (await client.Send(HttpMethod.Post, "/auth/login",
                new { email = client.Email, password = Client.Password })).StatusCode);

        var session = await LatestSession(client.Email);
        await using (var connection = new NpgsqlConnection(AppConnection()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "update identity.auth_session set authenticated_at = now() - interval '2 minutes', expires_at = now() - interval '1 minute' where id = @id",
                connection);
            command.Parameters.AddWithValue("id", session.Id);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.Raw("/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_current_server_session()
    {
        var client = new Client(factory);
        await client.Register();
        Assert.Equal(HttpStatusCode.OK,
            (await client.Send(HttpMethod.Post, "/auth/login",
                new { email = client.Email, password = Client.Password })).StatusCode);

        var session = await LatestSession(client.Email);
        Assert.Equal(HttpStatusCode.OK, (await client.Send(HttpMethod.Post, "/auth/logout", new { })).StatusCode);

        var afterLogout = await LatestSession(client.Email);
        Assert.NotNull(afterLogout.RevokedAt);
        Assert.Equal(session.Id, afterLogout.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.Raw("/me")).StatusCode);
    }
}
