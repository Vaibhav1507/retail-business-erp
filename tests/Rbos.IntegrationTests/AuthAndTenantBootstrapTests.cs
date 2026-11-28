using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rbos.Api.Application;
using System.Collections.Concurrent;

namespace Rbos.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
	public bool OpenRegistration { get; init; } = true;
	public RecordingEmailOtpSender EmailSender { get; } = new();
	protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
	{
		builder.UseEnvironment("Development");
		builder.UseSetting("Rbos:Database", "test");
		builder.UseSetting("Rbos:AllowOpenRegistration", OpenRegistration ? "true" : "false");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            // The integration suite exercises the offline/no-credential provider path.
            // Do not make external Vision API calls or spend model credits from CI.
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CloudVision:Enabled"] = "false",
                // Explicitly suppress both supported credential sources so CI never calls Anthropic.
                ["Anthropic:ApiKey"] = "",
                ["ANTHROPIC_API_KEY"] = ""
            });
        });
		builder.ConfigureServices(services =>
		{
			services.RemoveAll<IEmailOtpSender>();
			services.AddSingleton<IEmailOtpSender>(EmailSender);
		});
	}
}

public sealed class RecordingEmailOtpSender : IEmailOtpSender
{
    private readonly ConcurrentDictionary<string, string> codes = new(StringComparer.OrdinalIgnoreCase);
    public bool IsConfigured => true;
    public Task SendAsync(string email, string code, CancellationToken cancellationToken)
    {
        codes[email.Trim()] = code;
        return Task.CompletedTask;
    }
    public string? CodeFor(string email) => codes.TryGetValue(email.Trim(), out var code) ? code : null;
}

public sealed class Client
{
	readonly HttpClient http;
	public Client(ApiFactory f) => http = f.CreateClient();
	public Client(HttpClient client) => http = client;
	public string Email = $"user-{Guid.NewGuid():N}@example.test";
	public const string Password = "Synthetic#Pass123";

	public async Task<HttpResponseMessage> Send(HttpMethod m, string path, object? body = null, string? key = null)
	{
		var req = new HttpRequestMessage(m, "/api" + path);
		if (m != HttpMethod.Get)
		{
			var t = await http.GetFromJsonAsync<JsonElement>("/api/csrf");
			req.Headers.Add("X-CSRF-TOKEN", t.GetProperty("token").GetString());
		}
		if (key is not null) req.Headers.Add("Idempotency-Key", key);
		if (body is not null) req.Content = JsonContent.Create(body);
		return await http.SendAsync(req);
	}

	public async Task<HttpResponseMessage> SendBytes(string path, byte[] bytes, string contentType, string idempotencyKey, string? detectedBarcode = null, string? detectedSymbology = null)
	{
		var token = await http.GetFromJsonAsync<JsonElement>("/api/csrf");
		using var req = new HttpRequestMessage(HttpMethod.Post, "/api" + path);
		req.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
		req.Headers.Add("Idempotency-Key", idempotencyKey);
		if (!string.IsNullOrWhiteSpace(detectedBarcode)) req.Headers.Add("X-RBOS-Detected-Barcode", detectedBarcode);
		if (!string.IsNullOrWhiteSpace(detectedSymbology)) req.Headers.Add("X-RBOS-Detected-Symbology", detectedSymbology);
		req.Content = new ByteArrayContent(bytes);
		req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
		return await http.SendAsync(req);
	}

	public async Task Register()
	{
		var mobile = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999).ToString();
		var sent = await Send(HttpMethod.Post, "/auth/otp/send", new { mobile, purpose = "signup" });
		Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
		var code = (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("devCode").GetString();
		var ver = await Send(HttpMethod.Post, "/auth/otp/verify", new { mobile, code });
		Assert.Equal(HttpStatusCode.OK, ver.StatusCode);
		var proof = (await ver.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proof").GetGuid();
		Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Post, "/auth/register", new { email = Email, password = Password, mobile, otpProof = proof })).StatusCode);
	}
	public async Task SignUpAndBootstrap()
	{
		await Register();
		Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Post, "/auth/login", new { email = Email, password = Password })).StatusCode);
		var r = await Send(HttpMethod.Post, "/bootstrap", new { businessName = "Synthetic Traders", branchName = "Main", locationName = "Shop" }, Guid.NewGuid().ToString());
		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
	}

	public async Task<Guid> AddProduct(string name)
	{
		var r = await Send(HttpMethod.Post, "/products", new { name, categoryName = "Cables", isGeneric = true, baseUnit = "pcs" });
		Assert.Equal(HttpStatusCode.OK, r.StatusCode);
		return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
	}

	public async Task<Guid> Location()
	{
		var l = await http.GetFromJsonAsync<JsonElement>("/api/locations");
		return l[0].GetProperty("id").GetGuid();
	}

	public Task<HttpResponseMessage> SendRaw(HttpRequestMessage req) => http.SendAsync(req);
	public Task<JsonElement> GetJson(string path) => http.GetFromJsonAsync<JsonElement>("/api" + path);
	public Task<HttpResponseMessage> Raw(string path) => http.GetAsync("/api" + path);
}

public class AuthAndTenantBootstrapTests(ApiFactory f) : IClassFixture<ApiFactory>
{
	static string AppConnection()
	{
		var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
		return cfg["ConnectionStrings:RbosTestApp"]!;
	}

	[Fact]
	public async Task Unauthenticated_requests_are_rejected()
	{
		var c = new Client(f);
		Assert.Equal(HttpStatusCode.Unauthorized, (await c.Raw("/products")).StatusCode);
	}

	[Fact]
	public async Task Mutations_without_csrf_token_are_rejected()
	{
		using var http = f.CreateClient();
		var r = await http.PostAsJsonAsync("/api/auth/login", new { email = "a@example.test", password = "x" });
		Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
	}

	[Fact]
	public async Task Opening_stock_flow_posts_wac_and_is_idempotent()
	{
		var c = new Client(f);
		await c.SignUpAndBootstrap();
		var p = await c.AddProduct("USB-C cable 1m");
		var loc = await c.Location();
		var key = Guid.NewGuid().ToString();
		var body = new { locationId = loc, effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow), lines = new[] { new { productId = p, quantity = 10m, unitCost = 25.5m } } };

		var r1 = await c.Send(HttpMethod.Post, "/opening-stock", body, key);
		Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
		var j1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
		Assert.Equal(255.00m, j1.GetProperty("total").GetDecimal());

		var r2 = await c.Send(HttpMethod.Post, "/opening-stock", body, key);
		Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
		var j2 = await r2.Content.ReadFromJsonAsync<JsonElement>();
		Assert.True(j2.GetProperty("replayed").GetBoolean());
		Assert.Equal(j1.GetProperty("documentId").GetGuid(), j2.GetProperty("documentId").GetGuid());

		var other = new { locationId = loc, effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow), lines = new[] { new { productId = p, quantity = 1m, unitCost = 1m } } };
		Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/opening-stock", other, key)).StatusCode);

		var stock = await c.GetJson("/stock");
		Assert.Equal(1, stock.GetArrayLength());
		Assert.Equal(10m, stock[0].GetProperty("quantity").GetDecimal());
		Assert.Equal(25.5m, stock[0].GetProperty("wac").GetDecimal());
	}

	[Fact]
	public async Task Invalid_opening_stock_is_rejected()
	{
		var c = new Client(f);
		await c.SignUpAndBootstrap();
		var p = await c.AddProduct("Charger");
		var loc = await c.Location();
		var neg = new { locationId = loc, effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow), lines = new[] { new { productId = p, quantity = -1m, unitCost = 5m } } };
		Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/opening-stock", neg, Guid.NewGuid().ToString())).StatusCode);
		var noKey = new { locationId = loc, effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow), lines = new[] { new { productId = p, quantity = 1m, unitCost = 5m } } };
		Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/opening-stock", noKey)).StatusCode);
	}

	[Fact]
	public async Task Tenants_cannot_see_or_use_each_others_data()
	{
		var a = new Client(f); var b = new Client(f);
		await a.SignUpAndBootstrap(); await b.SignUpAndBootstrap();
		var pa = await a.AddProduct("Tenant A only");
		var locA = await a.Location();

		var bProducts = await b.GetJson("/products");
		Assert.Equal(0, bProducts.GetArrayLength());

		var attack = new { locationId = locA, effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow), lines = new[] { new { productId = pa, quantity = 1m, unitCost = 1m } } };
		Assert.Equal(HttpStatusCode.BadRequest, (await b.Send(HttpMethod.Post, "/opening-stock", attack, Guid.NewGuid().ToString())).StatusCode);
	}

	[Fact]
	public async Task App_role_without_context_sees_nothing_and_cannot_change_schema_or_delete_audit()
	{
		var c = new Client(f);
		await c.SignUpAndBootstrap();
		await c.AddProduct("Visible only with context");

		await using var conn = new NpgsqlConnection(AppConnection());
		await conn.OpenAsync();
		foreach (var table in new[] { "catalog.product", "core.tenant", "core.branch", "accounting.account", "inventory.stock_balance" })
		{
			await using var q = new NpgsqlCommand($"select count(*) from {table}", conn);
			Assert.Equal(0L, (long)(await q.ExecuteScalarAsync())!);
		}
		await Assert.ThrowsAsync<PostgresException>(async () => { await using var d = new NpgsqlCommand("create table public.evil(x int)", conn); await d.ExecuteNonQueryAsync(); });
		await Assert.ThrowsAsync<PostgresException>(async () => { await using var d = new NpgsqlCommand("delete from audit.audit_event", conn); await d.ExecuteNonQueryAsync(); });
		await Assert.ThrowsAsync<PostgresException>(async () => { await using var d = new NpgsqlCommand("update inventory.stock_movement set unit_cost = 1", conn); await d.ExecuteNonQueryAsync(); });
	}

	[Fact]
	public async Task Unbalanced_journal_cannot_commit()
	{
		var c = new Client(f);
		await c.SignUpAndBootstrap();
		var me = await c.GetJson("/me");
		var tid = me.GetProperty("tenantId").GetGuid();
		await using var conn = new NpgsqlConnection(AppConnection());
		await conn.OpenAsync();
		await using var tx = await conn.BeginTransactionAsync();
		var uid = await UserId(c.Email);
		await using (var ctx = new NpgsqlCommand("select set_config('app.user_id',@u,true), set_config('app.tenant_id',@t,true)", conn, tx))
		{ ctx.Parameters.AddWithValue("u", uid.ToString()); ctx.Parameters.AddWithValue("t", tid.ToString()); await ctx.ExecuteNonQueryAsync(); }
		var entry = Guid.NewGuid();
		await Exec(conn, tx, "insert into accounting.journal_entry(tenant_id,id,effective_date,source_type,source_id) values (@t,@e,current_date,'Test',@e)", tid, entry);
		await using (var l = new NpgsqlCommand("insert into accounting.journal_line(tenant_id,id,entry_id,account_id,debit,credit) select @t,gen_random_uuid(),@e,id,10,0 from accounting.account where code='1400'", conn, tx))
		{ l.Parameters.AddWithValue("t", tid); l.Parameters.AddWithValue("e", entry); await l.ExecuteNonQueryAsync(); }
		await Assert.ThrowsAsync<PostgresException>(() => tx.CommitAsync());
	}

	static async Task Exec(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, Guid t, Guid e)
	{
		await using var cmd = new NpgsqlCommand(sql, conn, tx);
		cmd.Parameters.AddWithValue("t", t); cmd.Parameters.AddWithValue("e", e);
		await cmd.ExecuteNonQueryAsync();
	}

	static async Task<Guid> UserId(string email)
	{
		var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
		await using var conn = new NpgsqlConnection(cfg["ConnectionStrings:RbosTestApp"]);
		await conn.OpenAsync();
		await using var cmd = new NpgsqlCommand("select \"Id\" from identity.\"AspNetUsers\" where \"NormalizedUserName\" = @n", conn);
		cmd.Parameters.AddWithValue("n", email.ToUpperInvariant());
		return (Guid)(await cmd.ExecuteScalarAsync())!;
	}
}

