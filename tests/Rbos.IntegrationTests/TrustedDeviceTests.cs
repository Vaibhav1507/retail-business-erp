using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Rbos.IntegrationTests;

public sealed class TrustedDeviceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string AppConnection()
    {
        var config = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        return config["ConnectionStrings:RbosTestApp"]!;
    }

    private static async Task<(Guid UserId, Guid DeviceId, Guid SessionId)> LatestDeviceSession(string email)
    {
        await using var connection = new NpgsqlConnection(AppConnection());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select u."Id", d.id, s.id
            from identity."AspNetUsers" u
            join identity.auth_session s on s.user_id = u."Id"
            join identity.auth_device d on d.id = s.device_id
            where u."NormalizedUserName" = @name
            order by s.created_at desc
            limit 1
            """, connection);
        command.Parameters.AddWithValue("name", email.ToUpperInvariant());
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Login must create a session linked to a registered device.");
        return (reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2));
    }

    [Fact]
    public async Task Password_login_registers_an_http_only_user_scoped_device_and_security_events()
    {
        var client = new Client(factory);
        await client.Register();

        var login = await client.Send(HttpMethod.Post, "/auth/login",
            new { email = client.Email, password = Client.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        Assert.Contains(login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("rbos.device=", StringComparison.Ordinal)
                && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)
                && value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));

        var persisted = await LatestDeviceSession(client.Email);
        var listed = await client.GetJson("/auth/devices");
        Assert.Equal(1, listed.GetArrayLength());
        Assert.Equal(persisted.DeviceId, listed[0].GetProperty("id").GetGuid());
        Assert.True(listed[0].GetProperty("isCurrentDevice").GetBoolean());
        Assert.Equal("Unknown browser on Unknown platform", listed[0].GetProperty("label").GetString());

        await using var connection = new NpgsqlConnection(AppConnection());
        await connection.OpenAsync();
        await using var events = new NpgsqlCommand("""
            select event_type
            from identity.auth_security_event
            where user_id = @user_id
            order by occurred_at
            """, connection);
        events.Parameters.AddWithValue("user_id", persisted.UserId);
        var types = new List<string>();
        await using var reader = await events.ExecuteReaderAsync();
        while (await reader.ReadAsync()) types.Add(reader.GetString(0));
        Assert.Contains("device.registered", types);
        Assert.Contains("session.created", types);
    }

    [Fact]
    public async Task Revoking_one_device_invalidates_its_sessions_but_not_another_device()
    {
        var first = new Client(factory);
        await first.Register();
        Assert.Equal(HttpStatusCode.OK, (await first.Send(HttpMethod.Post, "/auth/login",
            new { email = first.Email, password = Client.Password })).StatusCode);

        var second = new Client(factory) { Email = first.Email };
        Assert.Equal(HttpStatusCode.OK, (await second.Send(HttpMethod.Post, "/auth/login",
            new { email = second.Email, password = Client.Password })).StatusCode);

        var devices = await first.GetJson("/auth/devices");
        Assert.Equal(2, devices.GetArrayLength());
        var otherDevice = devices.EnumerateArray()
            .Single(item => !item.GetProperty("isCurrentDevice").GetBoolean());
        var otherDeviceId = otherDevice.GetProperty("id").GetGuid();

        var revoked = await first.Send(HttpMethod.Post, $"/auth/devices/{otherDeviceId}/revoke", new { });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first.Raw("/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.Raw("/me")).StatusCode);

        var after = await first.GetJson("/auth/devices");
        var revokedRecord = after.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == otherDeviceId);
        Assert.False(revokedRecord.GetProperty("revokedAt").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Device_list_is_scoped_to_the_authenticated_user_even_before_business_setup()
    {
        var first = new Client(factory);
        var second = new Client(factory);
        await first.Register();
        await second.Register();

        Assert.Equal(HttpStatusCode.OK, (await first.Send(HttpMethod.Post, "/auth/login",
            new { email = first.Email, password = Client.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.Send(HttpMethod.Post, "/auth/login",
            new { email = second.Email, password = Client.Password })).StatusCode);

        var firstDevices = await first.GetJson("/auth/devices");
        var secondDevices = await second.GetJson("/auth/devices");
        Assert.Equal(1, firstDevices.GetArrayLength());
        Assert.Equal(1, secondDevices.GetArrayLength());
        Assert.NotEqual(firstDevices[0].GetProperty("id").GetGuid(), secondDevices[0].GetProperty("id").GetGuid());

        var crossUserAttempt = await first.Send(HttpMethod.Post,
            $"/auth/devices/{secondDevices[0].GetProperty("id").GetGuid()}/revoke", new { });
        Assert.Equal(HttpStatusCode.NotFound, crossUserAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.Raw("/me")).StatusCode);
    }
}
