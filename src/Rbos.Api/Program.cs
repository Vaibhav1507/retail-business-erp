using Microsoft.EntityFrameworkCore;
using Rbos.Api.Persistence;

// Tooling modes (no web server). Normal startup is added in a later stage.
if (args.Contains("--emit-identity-sql"))
{
	var options = new DbContextOptionsBuilder<RbosIdentityDbContext>()
		.UseNpgsql("Host=localhost").Options;
	using var ctx = new RbosIdentityDbContext(options);
	var path = args[Array.IndexOf(args, "--emit-identity-sql") + 1];
	File.WriteAllText(path, ctx.Database.GenerateCreateScript());
	Console.WriteLine("Identity SQL written.");
	return;
}

if (args.Contains("--migrate"))
{
	var target = args[Array.IndexOf(args, "--migrate") + 1];
	var key = target switch { "dev" => "ConnectionStrings:RbosDevMigrator", "test" => "ConnectionStrings:RbosTestMigrator", "prod" => "ConnectionStrings:RbosProdMigrator", _ => throw new ArgumentException("target must be dev, test or prod") };
	var config = new ConfigurationBuilder().AddUserSecrets<RbosIdentityDbContext>().AddEnvironmentVariables().Build();
	var cs = config[key] ?? throw new InvalidOperationException($"Missing secret {key}");
	var applied = await DatabaseMigrator.ApplyAsync(cs);
	Console.WriteLine(applied.Count == 0 ? "No pending migrations." : "Applied: " + string.Join(", ", applied));
	return;
}

var builder = WebApplication.CreateBuilder(args);
var dbKey = (builder.Configuration["Rbos:Database"] ?? "dev") switch { "test" => "ConnectionStrings:RbosTestApp", "prod" => "ConnectionStrings:RbosProdApp", _ => "ConnectionStrings:RbosDevApp" };
var appCs = builder.Configuration[dbKey] ?? throw new InvalidOperationException($"Missing secret {dbKey}");
builder.Services.AddSingleton(Npgsql.NpgsqlDataSource.Create(appCs));
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
	o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
	o.KnownIPNetworks.Clear();
	o.KnownProxies.Clear();
});
builder.Services.AddDataProtection();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>>(sp =>
	new Microsoft.Extensions.Options.ConfigureOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(o => o.XmlRepository = new Rbos.Api.Persistence.PgXmlRepository(sp.GetRequiredService<Npgsql.NpgsqlDataSource>())));
builder.Services.AddScoped<Rbos.Api.Persistence.TenantDb>();
builder.Services.AddScoped<Rbos.Api.Application.OpeningStockService>();
builder.Services.AddScoped<Rbos.Api.Application.CatalogueService>();
	builder.Services.AddScoped<Rbos.Api.Application.CategoryAttributeService>();
builder.Services.AddScoped<Rbos.Api.Application.BarcodeService>();
builder.Services.AddScoped<Rbos.Api.Application.BrandService>();
builder.Services.AddHttpClient<Rbos.Api.Application.IProductLookupProvider, Rbos.Api.Application.OpenFoodFactsProvider>(h => h.Timeout = TimeSpan.FromSeconds(6));
builder.Services.AddScoped<Rbos.Api.Application.ExternalBarcodeLookupCache>();
builder.Services.AddScoped<Rbos.Api.Application.ProductLookupCoordinator>();
builder.Services.AddScoped<Rbos.Api.Application.ProcurementService>();
builder.Services.AddHttpClient<Rbos.Api.Application.InvoiceExtractionService>(h => h.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddSingleton<Rbos.Api.Application.LocalOcr>();
builder.Services.AddHttpClient<Rbos.Api.Application.ProductFileReader>(h => h.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<Rbos.Api.Application.OtpService>();
if (builder.Configuration["Sms:Provider"] == "msg91") builder.Services.AddHttpClient<Rbos.Api.Application.ISmsSender, Rbos.Api.Application.Msg91SmsSender>(h => h.Timeout = TimeSpan.FromSeconds(15));
else if (builder.Environment.IsDevelopment()) builder.Services.AddSingleton<Rbos.Api.Application.ISmsSender, Rbos.Api.Application.DevSmsSender>();
else if (!Rbos.Api.AuthOtpEndpoints.OtpEnabled(builder.Configuration)) builder.Services.AddSingleton<Rbos.Api.Application.ISmsSender, Rbos.Api.Application.DisabledSmsSender>();
else throw new InvalidOperationException("Sms:Provider must be configured outside Development, or set Otp:Enabled=false.");
builder.Services.AddScoped<Rbos.Api.Application.SalesService>();
builder.Services.AddScoped<Rbos.Api.Application.AfterSalesService>();
builder.Services.AddScoped<Rbos.Api.Application.FinanceService>();
builder.Services.AddScoped<Rbos.Api.Application.StockInsightsService>();
builder.Services.AddScoped<Rbos.Api.Application.CustomerInsightsService>();
builder.Services.AddScoped<Rbos.Api.Application.DashboardService>(); builder.Services.AddScoped<Rbos.Api.Application.ReminderService>();
builder.Services.AddScoped<Rbos.Api.Application.AuditHistoryService>();
builder.Services.AddDbContext<RbosIdentityDbContext>(o => o.UseNpgsql(appCs));
builder.Services.AddIdentity<Microsoft.AspNetCore.Identity.IdentityUser<Guid>, Microsoft.AspNetCore.Identity.IdentityRole<Guid>>(o =>
{
	o.Password.RequiredLength = 12;
	o.Lockout.MaxFailedAccessAttempts = 5;
	o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
	o.User.RequireUniqueEmail = true;
}).AddEntityFrameworkStores<RbosIdentityDbContext>();
builder.Services.ConfigureApplicationCookie(o =>
{
	o.Cookie.Name = "rbos.auth";
	o.Cookie.HttpOnly = true;
	o.Cookie.SameSite = SameSiteMode.Strict;
	o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
	o.ExpireTimeSpan = TimeSpan.FromHours(8);
	o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
	o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; });
builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddCheck<Rbos.Api.DatabaseHealthCheck>("database");
var app = builder.Build();
app.UseForwardedHeaders();
app.MapOpenApi("/api/openapi/{documentName}.json").AllowAnonymous();
await using (var probe = await app.Services.GetRequiredService<Npgsql.NpgsqlDataSource>().OpenConnectionAsync())
await using (var who = new Npgsql.NpgsqlCommand("select current_user", probe))
{
	if ((string?)await who.ExecuteScalarAsync() != "rbos_app") throw new InvalidOperationException("Runtime must connect as rbos_app.");
}
app.Use(async (c, next) =>
{
	var id = c.Request.Headers.TryGetValue("X-Correlation-ID", out var h) && h.ToString().Length is > 0 and <= 64 ? h.ToString() : Guid.NewGuid().ToString("N");
	c.Response.Headers["X-Correlation-ID"] = id;
	c.TraceIdentifier = id;
	using (app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id })) await next();
});
app.UseExceptionHandler(a => a.Run(async c =>
{
	var ex = c.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
	if (ex is not null) app.Logger.LogError(ex, "Unhandled exception {Method} {Path} correlation {CorrelationId}", c.Request.Method, c.Request.Path, c.TraceIdentifier);
	c.Response.StatusCode = 500;
	c.Response.ContentType = "application/problem+json";
	await c.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
	{
		Status = 500,
		Title = "Unexpected error. Please retry.",
		Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
		Extensions = { ["error"] = "Unexpected error. Please retry.", ["traceId"] = c.TraceIdentifier }
	}, options: null, contentType: "application/problem+json");
}));
if (!app.Environment.IsDevelopment())
{
	app.UseHsts();
	app.UseHttpsRedirection();
}
app.UseMiddleware<Rbos.Api.Security.SecurityMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/api/health").AllowAnonymous();
Rbos.Api.Endpoints.Map(app);
app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();
app.Run();

public partial class Program { }

