using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Rbos.IntegrationTests;

public class SecurityControlTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Api_responses_have_security_headers_and_no_store()
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        var r = await c.Raw("/products");
        Assert.Equal("no-store", r.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", r.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Auth_cookie_is_httponly_and_samesite_strict()
    {
        var c = new Client(f);
        var email = c.Email;
        await c.Register();
        var r = await c.Send(HttpMethod.Post, "/auth/login", new { email, password = Client.Password });
        var cookie = r.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("rbos.auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Oversized_body_is_rejected()
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        var big = new { name = new string('x', 100_000), categoryName = "c", isGeneric = true, baseUnit = "pcs" };
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await c.Send(HttpMethod.Post, "/products", big)).StatusCode);
    }

    [Fact]
    public async Task Non_json_content_type_is_rejected()
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/products") { Content = new StringContent("name=x", Encoding.UTF8, "text/plain") };
        var token = (await c.GetJson("/csrf")).GetProperty("token").GetString();
        req.Headers.Add("X-CSRF-TOKEN", token);
        var r = await c.SendRaw(req);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, r.StatusCode);
    }

    [Fact]
    public async Task Duplicate_registration_does_not_reveal_account_existence()
    {
        var c = new Client(f);
        await c.Send(HttpMethod.Post, "/auth/register", new { email = c.Email, password = Client.Password });
        var again = await c.Send(HttpMethod.Post, "/auth/register", new { email = c.Email, password = Client.Password });
        var body = await again.Content.ReadAsStringAsync();
        Assert.DoesNotContain("taken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("already", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_and_wrong_password_logins_return_identical_responses()
    {
        var c = new Client(f);
        await c.Send(HttpMethod.Post, "/auth/register", new { email = c.Email, password = Client.Password });
        var wrong = await c.Send(HttpMethod.Post, "/auth/login", new { email = c.Email, password = "Wrong#Pass12345" });
        var unknown = await c.Send(HttpMethod.Post, "/auth/login", new { email = "nobody-" + Guid.NewGuid().ToString("N") + "@example.test", password = "Wrong#Pass12345" });
        Assert.Equal(wrong.StatusCode, unknown.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Registration_is_closed_by_default_once_a_user_exists()
    {
        using var closed = new ApiFactory { OpenRegistration = false };
        var c = new Client(closed);
        var r = await c.Send(HttpMethod.Post, "/auth/register", new { email = c.Email, password = Client.Password });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }
}

public class MembershipIsolationTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static NpgsqlConnection Migrator()
    {
        var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
        return new NpgsqlConnection(cfg["ConnectionStrings:RbosTestMigrator"]);
    }

    [Fact]
    public async Task No_user_directory_or_membership_listing_endpoint_exists()
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        foreach (var path in new[] { "/users", "/memberships", "/tenants", "/members", "/auth/users" })
            Assert.True((await c.Raw(path)).StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, path);
    }

    [Fact]
    public async Task App_role_sees_only_own_membership_rows()
    {
        var a = new Client(f); var b = new Client(f);
        await a.SignUpAndBootstrap(); await b.SignUpAndBootstrap();
        var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
        await using var conn = new NpgsqlConnection(cfg["ConnectionStrings:RbosTestApp"]);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var none = new NpgsqlCommand("select count(*) from core.tenant_membership", conn, tx))
            Assert.Equal(0L, (long)(await none.ExecuteScalarAsync())!);

        Guid uid;
        await using (var m = Migrator())
        {
            await m.OpenAsync();
            await using var q = new NpgsqlCommand("select \"Id\" from identity.\"AspNetUsers\" where \"UserName\"=@e", m);
            q.Parameters.AddWithValue("e", a.Email);
            uid = (Guid)(await q.ExecuteScalarAsync())!;
        }
        using var scope = f.Services.CreateScope();
        var tid = (await scope.ServiceProvider.GetRequiredService<Rbos.Api.Persistence.TenantDb>().FindTenantAsync(uid, default))!.Value;
        await using (var set = new NpgsqlCommand("select set_config('app.user_id',@u,true), set_config('app.tenant_id',@t,true)", conn, tx))
        {
            set.Parameters.AddWithValue("u", uid.ToString()); set.Parameters.AddWithValue("t", tid.ToString());
            await set.ExecuteNonQueryAsync();
        }
        await using var own = new NpgsqlCommand("select count(*), count(*) filter (where user_id <> @u) from core.tenant_membership", conn, tx);
        own.Parameters.AddWithValue("u", uid);
        await using var rd = await own.ExecuteReaderAsync();
        await rd.ReadAsync();
        Assert.Equal(1L, rd.GetInt64(0));
        Assert.Equal(0L, rd.GetInt64(1));
    }

    [Fact]
    public async Task Revoked_membership_loses_access()
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        Assert.Equal(HttpStatusCode.OK, (await c.Raw("/products")).StatusCode);
        await using (var m = Migrator())
        {
            await m.OpenAsync();
            await using var tx = await m.BeginTransactionAsync();
            await using (var d = new NpgsqlCommand("alter table core.tenant_membership no force row level security", m, tx)) await d.ExecuteNonQueryAsync();
            await using (var u = new NpgsqlCommand("update core.tenant_membership set status='Revoked' where user_id=(select \"Id\" from identity.\"AspNetUsers\" where \"UserName\"=@e)", m, tx))
            {
                u.Parameters.AddWithValue("e", c.Email);
                Assert.Equal(1, await u.ExecuteNonQueryAsync());
            }
            await using (var d = new NpgsqlCommand("alter table core.tenant_membership force row level security", m, tx)) await d.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await c.Raw("/products")).StatusCode);
        var body = await c.Send(HttpMethod.Post, "/products", new { name = "x", categoryName = "c", isGeneric = true, baseUnit = "pcs" });
        Assert.Equal(HttpStatusCode.Conflict, body.StatusCode);
    }
}
