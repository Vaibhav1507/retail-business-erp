using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rbos.Api.Application;
using Rbos.Api.Persistence;
using Rbos.Domain;

namespace Rbos.Api;

public sealed record Credentials(string Email, string Password);
public sealed record BootstrapRequest(string BusinessName, string BranchName, string LocationName);
public sealed record ProductRequest(string Name, string CategoryName, bool IsGeneric, string BaseUnit, string? Brand, string? Model, Dictionary<string, string>? Attributes);
public sealed record StockLine(Guid ProductId, decimal Quantity, decimal UnitCost);
public sealed record OpeningStockRequest(Guid LocationId, DateOnly EffectiveDate, List<StockLine> Lines);

public static class Endpoints
{
	static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, (DateOnly Day, int Count)> WhisperDailyUsage = new();
	static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, (DateOnly Day, int Count)> VoiceIntentDailyUsage = new();
	const int MaxVoiceIntentRequestsPerUserPerDay = 100;
	const int MaxWhisperRequestsPerUserPerDay = 100;
	static readonly string[] Units = ["pcs", "set", "pack", "m"];
	static readonly string[] AttrKeys = ["colour", "capacity", "connector_type", "compatible_with"];

	public static void Map(WebApplication app)
	{
		app.MapGet("/healthz", () => Results.Ok()).AllowAnonymous();
		app.Use(async (ctx, next) =>
		{
			if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method))
			{
				try { await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx); }
				catch (AntiforgeryValidationException) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { error = "Invalid CSRF token." }); return; }
			}
			await next();
		});

		var api = app.MapGroup("/api");
		PermissionFilter.Apply(api);
		api.MapGet("/csrf", (HttpContext c, IAntiforgery af) => Results.Ok(new { token = af.GetAndStoreTokens(c).RequestToken })).AllowAnonymous();

		api.MapGet("/voice/intent/config", (VoiceNavigationIntentService intent) =>
			Results.Ok(new { enabled = intent.Available })).RequireAuthorization();

		api.MapPost("/voice/intent", async (VoiceIntentRequest body, ClaimsPrincipal principal, VoiceNavigationIntentService intent, CancellationToken ct) =>
		{
			if (!intent.Available) return ApiResults.Fail(503, "AI navigation intent is not enabled.");
			if (string.IsNullOrWhiteSpace(body.Transcript) || body.Transcript.Trim().Length > 500)
				return ApiResults.Fail(400, "Voice transcript must be between 1 and 500 characters.");
			var userId = UserId(principal);
			var today = DateOnly.FromDateTime(DateTime.UtcNow);
			var usage = VoiceIntentDailyUsage.AddOrUpdate(userId, _ => (today, 1), (_, current) => current.Day == today ? (today, current.Count + 1) : (today, 1));
			if (usage.Count > MaxVoiceIntentRequestsPerUserPerDay)
				return ApiResults.Fail(429, "Daily AI navigation limit reached. Use the basic navigation commands and try again tomorrow.");
			var result = await intent.ClassifyAsync(body, ct);
			return Results.Ok(result);
		}).RequireAuthorization();

		api.MapGet("/voice/transcribe/config", (GroqSpeechToTextClient whisper) =>
			Results.Ok(new { enabled = whisper.Available })).RequireAuthorization();

		api.MapPost("/voice/transcribe", async (HttpRequest request, ClaimsPrincipal principal, GroqSpeechToTextClient whisper, CancellationToken ct) =>
		{
			if (!whisper.Available) return ApiResults.Fail(503, "Hosted speech fallback is not enabled.");
			if (request.ContentLength is <= 44 or > 640_044) return ApiResults.Fail(400, "Voice command audio must be a WAV under 1 MB.");
			await using var buffer = new MemoryStream();
			var chunk = new byte[81920];
			while (true)
			{
				var read = await request.Body.ReadAsync(chunk, ct);
				if (read == 0) break;
				if (buffer.Length + read > 640_044) return ApiResults.Fail(400, "Voice command audio must be a WAV under 1 MB.");
				await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
			}
			if (buffer.Length is <= 44 or > 640_044) return ApiResults.Fail(400, "Voice command audio must be a WAV under 1 MB.");
			var wav = buffer.ToArray();
			if (wav[0] != (byte)'R' || wav[1] != (byte)'I' || wav[2] != (byte)'F' || wav[3] != (byte)'F' ||
				wav[8] != (byte)'W' || wav[9] != (byte)'A' || wav[10] != (byte)'V' || wav[11] != (byte)'E' ||
				wav[12] != (byte)'f' || wav[13] != (byte)'m' || wav[14] != (byte)'t' || wav[15] != (byte)' ' ||
				BitConverter.ToUInt32(wav, 4) != (uint)(wav.Length - 8) ||
				BitConverter.ToUInt32(wav, 16) != 16 || BitConverter.ToUInt16(wav, 20) != 1 || BitConverter.ToUInt16(wav, 22) != 1 ||
				BitConverter.ToUInt32(wav, 24) != 16000 || BitConverter.ToUInt32(wav, 28) != 32000 ||
				BitConverter.ToUInt16(wav, 32) != 2 || BitConverter.ToUInt16(wav, 34) != 16 ||
				BitConverter.ToUInt32(wav, 40) != (uint)(wav.Length - 44))
				return ApiResults.Fail(400, "Voice command audio must be mono 16 kHz PCM WAV.");
			var userId = UserId(principal);
			var today = DateOnly.FromDateTime(DateTime.UtcNow);
			var usage = WhisperDailyUsage.AddOrUpdate(userId, _ => (today, 1), (_, current) => current.Day == today ? (today, current.Count + 1) : (today, 1));
			if (usage.Count > MaxWhisperRequestsPerUserPerDay) return ApiResults.Fail(429, "Daily voice backup limit reached. Please type the command and try again tomorrow.");
			try
			{
				var transcript = await whisper.TranscribeWavAsync(wav, ct);
				return string.IsNullOrWhiteSpace(transcript) ? ApiResults.Fail(422, "Speech could not be transcribed.") : Results.Ok(new { text = transcript });
			}
			catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
			{ return ApiResults.Fail(429, "Speech backup limit reached. Please type the command or try again later."); }
			catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
			{ return ApiResults.Fail(503, "Speech backup is temporarily unavailable. Please type the command or try again."); }
			finally
			{
				CryptographicOperations.ZeroMemory(wav);
				CryptographicOperations.ZeroMemory(chunk);
				if (buffer.TryGetBuffer(out var audioBuffer)) CryptographicOperations.ZeroMemory(audioBuffer.AsSpan());
			}
		}).RequireAuthorization();

		AuthOtpEndpoints.Map(api);

		api.MapPost("/auth/login", async (Credentials m, UserManager<IdentityUser<Guid>> um, SignInManager<IdentityUser<Guid>> sm, IWebHostEnvironment env, AuthSessionService sessions, HttpContext context, CancellationToken ct) =>
		{
			var u = await um.FindByNameAsync(m.Email?.Trim() ?? "");
			if (u is null) return ApiResults.Fail(401, "Invalid credentials.");
			// TEMPORARY (testing only): passwordless demo login, Development environment only. Remove before release.
			if (env.IsDevelopment() && string.IsNullOrEmpty(m.Password) && u.UserName == "demo.electronics@example.test")
			{ await sessions.SignInAsync(context, u, sm, ct); return Results.Ok(); }
			var r = await sm.CheckPasswordSignInAsync(u, m.Password ?? "", lockoutOnFailure: true);
			if (!r.Succeeded)
				return ApiResults.Fail(401, "Invalid credentials.");
			await sessions.SignInAsync(context, u, sm, ct);
			return Results.Ok();
		}).AllowAnonymous();

		api.MapPost("/auth/logout", async (SignInManager<IdentityUser<Guid>> sm, AuthSessionService sessions, HttpContext context, CancellationToken ct) => { await sessions.RevokeCurrentAsync(context.User, ct); await sm.SignOutAsync(); return Results.Ok(); }).RequireAuthorization();

		api.MapGet("/me", async (ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
		{
			var uid = UserId(p);
			var tid = await db.FindTenantAsync(uid, ct);
			var done = false; string? category = null;
			if (tid is not null)
			{
				await using var s = await db.BeginAsync(uid, tid, ct);
				await using var c = s.Command("select onboarding_completed, business_category from core.business_profile");
				await using var r = await c.ExecuteReaderAsync(ct);
				if (await r.ReadAsync(ct)) { done = r.GetBoolean(0); category = r.IsDBNull(1) ? null : r.GetString(1); }
			}
			var role = await db.FindRoleAsync(uid, ct);
			return Results.Ok(new { email = p.Identity?.Name, tenantId = tid, profileComplete = done, businessCategory = category, role, permissions = Permissions.For(role) });
		}).RequireAuthorization();

		api.MapPost("/bootstrap", async (BootstrapRequest m, HttpRequest req, ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
		{
			var key = req.Headers["Idempotency-Key"].ToString();
			if (key.Length is < 8 or > 100) return ApiResults.Fail(400, "Idempotency-Key header (8-100 chars) required.");
			if (Blank(m.BusinessName, 120) || Blank(m.BranchName, 120) || Blank(m.LocationName, 120)) return ApiResults.Fail(400, "Names are required (max 120 chars).");
			var uid = UserId(p);
			var existing = await db.FindTenantAsync(uid, ct);
			if (existing is not null) return Results.Ok(new { tenantId = existing, replayed = true });

			var tid = Guid.NewGuid();
			try
			{
			await using var s = await db.BeginAsync(uid, tid, ct);
			await Exec(s, "insert into core.tenant(id,name,created_by,bootstrap_key) values (@a,@n,@u,@k)", ("a", tid), ("n", m.BusinessName.Trim()), ("u", uid), ("k", key));
			await Exec(s, "insert into core.tenant_membership(tenant_id,user_id,role,status) values (@a,@u,'Owner','Active')", ("a", tid), ("u", uid));
			await Exec(s, "insert into core.business_profile(tenant_id,display_name) values (@a,@n)", ("a", tid), ("n", m.BusinessName.Trim()));
			var branch = Guid.NewGuid();
			await Exec(s, "insert into core.branch(tenant_id,id,name) values (@a,@b,@n)", ("a", tid), ("b", branch), ("n", m.BranchName.Trim()));
			await Exec(s, "insert into core.stock_location(tenant_id,id,branch_id,name) values (@a,@l,@b,@n)", ("a", tid), ("l", Guid.NewGuid()), ("b", branch), ("n", m.LocationName.Trim()));
			await Exec(s, "insert into accounting.account(tenant_id,id,code,name,type) values (@a,@i,'1400','Inventory','Asset'),(@a,@j,'3900','Opening Balance Equity','Equity')", ("a", tid), ("i", Guid.NewGuid()), ("j", Guid.NewGuid()));
			await s.CommitAsync(ct);
			return Results.Ok(new { tenantId = tid, replayed = false });
			}
			catch (PostgresException e) when (e.SqlState == "23505")
			{
				var again = await db.FindTenantAsync(uid, ct);
				return again is null ? ApiResults.Fail(409, "Business already exists for this user.") : Results.Ok(new { tenantId = again, replayed = true });
			}
		}).RequireAuthorization();

		api.MapGet("/locations", async (ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
		{
			var (s, tid) = await Open(p, db, ct); if (s is null) return ApiResults.Fail(409, "No business set up.");
			await using var _ = s;
			await using var c = s.Command("select id,name from core.stock_location order by name");
			return Results.Ok(await Rows(c, ct, r => new { id = r.GetGuid(0), name = r.GetString(1) }));
		}).RequireAuthorization();

		ProfileEndpoints.Map(api);
		CatalogueEndpoints.Map(api);
		BrandEndpoints.Map(api);
		BarcodeEndpoints.Map(api);
		ProcurementEndpoints.Map(api);
        SalesEndpoints.Map(api);
                AfterSalesEndpoints.Map(api);
                FinanceEndpoints.Map(api);
                ReportChecksEndpoints.Map(api);
                GstEndpoints.Map(api);
                StaffEndpoints.Map(api);
                BranchEndpoints.Map(api);
                LocationMoveEndpoints.Map(api);
        OrderingEndpoints.Map(api);
        BankMatchEndpoints.Map(api);
                StockEndpoints.Map(api);
        CustomerInsightsEndpoints.Map(api);
        DashboardEndpoints.Map(api); ReminderEndpoints.Map(api);
                AuditEndpoints.Map(api);

		api.MapPost("/opening-stock", async (OpeningStockRequest m, HttpRequest req, ClaimsPrincipal p, OpeningStockService svc, CancellationToken ct) =>
		{
			var lines = m.Lines?.Select(x => new OpeningStockLineInput(x.ProductId, x.Quantity, x.UnitCost)).ToList();
			var o = await svc.PostAsync(new PostOpeningStockCommand(UserId(p), req.Headers["Idempotency-Key"].ToString(), m.LocationId, m.EffectiveDate, lines, req.HttpContext.TraceIdentifier), ct);
			return o.Status switch
			{
				OpeningStockStatus.Posted => Results.Ok(new { documentId = o.DocumentId, total = o.Total, replayed = false }),
				OpeningStockStatus.Replayed => Results.Ok(new { documentId = o.DocumentId, replayed = true }),
				OpeningStockStatus.Invalid => ApiResults.Fail(400, o.Error),
				_ => ApiResults.Fail(409, o.Error),
			};
		}).RequireAuthorization();

		api.MapGet("/stock", async (ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
		{
			var (s, _) = await Open(p, db, ct); if (s is null) return ApiResults.Fail(409, "No business set up.");
			await using var __ = s;
			await using var c = s.Command("select p.id,p.name,p.internal_sku,l.name,b.quantity,ic.wac,ic.inventory_value,cat.name,(select array_agg(distinct sp.name order by sp.name) from procurement.purchase_receipt_line rl join procurement.purchase_receipt pr on pr.tenant_id=rl.tenant_id and pr.id=rl.receipt_id join procurement.supplier sp on sp.tenant_id=pr.tenant_id and sp.id=pr.supplier_id where rl.tenant_id=b.tenant_id and rl.product_id=b.product_id) from inventory.stock_balance b join catalog.product p on p.tenant_id=b.tenant_id and p.id=b.product_id join catalog.category cat on cat.tenant_id=p.tenant_id and cat.id=p.category_id join core.stock_location l on l.tenant_id=b.tenant_id and l.id=b.location_id join inventory.item_cost ic on ic.tenant_id=b.tenant_id and ic.product_id=b.product_id order by p.name");
			return Results.Ok(await Rows(c, ct, r => new { productId = r.GetGuid(0), name = r.GetString(1), sku = r.GetString(2), location = r.GetString(3), quantity = r.GetDecimal(4), wac = r.GetDecimal(5), value = r.GetDecimal(6), category = r.GetString(7), suppliers = r.IsDBNull(8) ? Array.Empty<string>() : r.GetFieldValue<string[]>(8) }));
		}).RequireAuthorization();
	}

	static Guid UserId(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
	static bool Blank(string? v, int max) => string.IsNullOrWhiteSpace(v) || v.Trim().Length > max;
	static string? NullIfBlank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

	static async Task<(TenantSession? s, Guid tid)> Open(ClaimsPrincipal p, TenantDb db, CancellationToken ct)
	{
		var uid = UserId(p);
		var tid = await db.FindTenantAsync(uid, ct);
		if (tid is null) return (null, default);
		return (await db.BeginAsync(uid, tid, ct), tid.Value);
	}

	static async Task Exec(TenantSession s, string sql, params (string, object?)[] ps)
	{
		await using var c = s.Command(sql);
		foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
		await c.ExecuteNonQueryAsync();
	}

	static async Task<T?> Scalar<T>(TenantSession s, string sql, CancellationToken ct, params (string, object?)[] ps)
	{
		await using var c = s.Command(sql);
		foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
		var r = await c.ExecuteScalarAsync(ct);
		return r is null or DBNull ? default : (T)r;
	}

	static async Task<List<T>> Rows<T>(NpgsqlCommand c, CancellationToken ct, Func<NpgsqlDataReader, T> map)
	{
		var list = new List<T>();
		await using var r = await c.ExecuteReaderAsync(ct);
		while (await r.ReadAsync(ct)) list.Add(map(r));
		return list;
	}
}

