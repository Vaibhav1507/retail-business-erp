using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Rbos.Api;
using System.Security.Claims;
using Rbos.Api.Persistence;
using Rbos.Api.Application;

// Tooling modes (no web server). Normal startup is added in a later stage.
// --rbos-workers runs both bounded queues; the specific flags remain available for
// targeted operations and preserve the legacy WhatsApp-only command.
var runAllWorkers = args.Contains("--rbos-workers");
var runWhatsAppWorker = args.Contains("--whatsapp-worker") || runAllWorkers;
var runDocumentWorker = args.Contains("--document-worker") || runAllWorkers;
if (runWhatsAppWorker || runDocumentWorker)
{
    var config = new ConfigurationBuilder().AddUserSecrets<RbosIdentityDbContext>().AddEnvironmentVariables().Build();
    var database = config["Rbos:Database"] ?? "dev";
    var appKey = database switch { "test" => "ConnectionStrings:RbosTestApp", "prod" => "ConnectionStrings:RbosProdApp", _ => "ConnectionStrings:RbosDevApp" };
    // Both queue processors use the existing least-privilege rbos_worker role/secret.
    var workerKey = database switch { "test" => "ConnectionStrings:RbosTestWhatsAppWorker", "prod" => "ConnectionStrings:RbosProdWhatsAppWorker", _ => "ConnectionStrings:RbosDevWhatsAppWorker" };
    var appConnectionString = config[appKey] ?? throw new InvalidOperationException($"Missing secret {appKey}");
    var workerConnectionString = config[workerKey] ?? throw new InvalidOperationException($"Missing secret {workerKey}");
    await using var dataSource = Npgsql.NpgsqlDataSource.Create(appConnectionString);
    await using var queueDataSource = Npgsql.NpgsqlDataSource.Create(workerConnectionString);
    await using (var probe = await dataSource.OpenConnectionAsync())
    await using (var who = new Npgsql.NpgsqlCommand("select current_user", probe))
        if ((string?)await who.ExecuteScalarAsync() != "rbos_app")
            throw new InvalidOperationException("Worker tenant data connection must use rbos_app.");
    await using (var probe = await queueDataSource.OpenConnectionAsync())
    await using (var who = new Npgsql.NpgsqlCommand("select current_user", probe))
        if ((string?)await who.ExecuteScalarAsync() != "rbos_worker")
            throw new InvalidOperationException("Worker queue connection must use rbos_worker.");

    using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    using var sigterm = System.Runtime.InteropServices.PosixSignalRegistration.Create(
        System.Runtime.InteropServices.PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        });

    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    QuestPDF.Settings.UseSystemFonts = true;
    if (runDocumentWorker)
    {
        var workerServices = new ServiceCollection();
        workerServices.AddLogging(logging => logging.AddConsole());
        workerServices.AddSingleton<IConfiguration>(config);
        workerServices.AddSingleton(dataSource);
        workerServices.AddScoped<TenantDb>();
        workerServices.AddSingleton<LocalOcr>();
        workerServices.AddHttpClient<GroqVisionClient>(http => http.Timeout = TimeSpan.FromSeconds(90));
        workerServices.AddHttpClient<SalesInvoiceCaptureService>(http => http.Timeout = TimeSpan.FromSeconds(90));
        workerServices.AddHttpClient<InvoiceExtractionService>(http => http.Timeout = TimeSpan.FromSeconds(90));
        var serviceProvider = new DefaultServiceProviderFactory().CreateServiceProvider(workerServices);
        try
        {
            await DocumentProcessingWorker.RunAsync(
                queueDataSource, serviceProvider,
                loggerFactory.CreateLogger("DocumentProcessingWorker"), cancellation.Token);
        }
        finally
        {
            (serviceProvider as IDisposable)?.Dispose();
        }
    }
    if (runWhatsAppWorker)
        await WhatsAppInvoiceWorker.RunAsync(dataSource, queueDataSource, config,
            loggerFactory.CreateLogger("WhatsAppInvoiceWorker"), cancellation.Token);
    return;
}

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

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
QuestPDF.Settings.UseSystemFonts = true;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
var passkeyDomain = builder.Configuration["Auth:Passkeys:ServerDomain"]?.Trim().ToLowerInvariant();
if (!string.IsNullOrWhiteSpace(passkeyDomain))
{
    if (Uri.CheckHostName(passkeyDomain) != UriHostNameType.Dns)
        throw new InvalidOperationException("Auth:Passkeys:ServerDomain must be a valid DNS host name.");
    var allowedHosts = builder.Configuration["AllowedHosts"];
    var configuredHosts = allowedHosts?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    if (configuredHosts.Length == 0 || (configuredHosts.Length == 1 && configuredHosts[0] == "*"))
        builder.Configuration["AllowedHosts"] = passkeyDomain;
    else if (configuredHosts.Length != 1 || !configuredHosts[0].Equals(passkeyDomain, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("When passkeys are enabled, AllowedHosts must contain only the exact Auth:Passkeys:ServerDomain host. Wildcards and alternate hosts are not allowed.");
    builder.Services.Configure<IdentityPasskeyOptions>(options =>
    {
        options.ServerDomain = passkeyDomain;
        options.UserVerificationRequirement = "required";
        options.ResidentKeyRequirement = "required";
    });
}
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
builder.Services.AddSingleton<AuthSessionService>();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>>(sp =>
	new Microsoft.Extensions.Options.ConfigureOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(o => o.XmlRepository = new Rbos.Api.Persistence.PgXmlRepository(sp.GetRequiredService<Npgsql.NpgsqlDataSource>())));
builder.Services.AddScoped<Rbos.Api.Persistence.TenantDb>();
builder.Services.AddScoped<Rbos.Api.Application.OpeningStockService>();
builder.Services.AddScoped<Rbos.Api.Application.CatalogueService>();
builder.Services.AddScoped<Rbos.Api.Application.BarcodeService>();
builder.Services.AddScoped<Rbos.Api.Application.BrandService>();
builder.Services.AddHttpClient<Rbos.Api.Application.IProductLookupProvider, Rbos.Api.Application.OpenFoodFactsProvider>(h => h.Timeout = TimeSpan.FromSeconds(6));
builder.Services.AddScoped<Rbos.Api.Application.ExternalBarcodeLookupCache>();
builder.Services.AddScoped<Rbos.Api.Application.ProductLookupCoordinator>();
builder.Services.AddScoped<Rbos.Api.Application.ProcurementService>();
builder.Services.AddScoped<Rbos.Api.Application.DocumentIntakeService>();
builder.Services.AddSingleton<Rbos.Api.Application.LocalOcr>();
builder.Services.AddHttpClient("AiGateway.Gemini", h => h.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddHttpClient("AiGateway.Groq", h => h.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddSingleton<Rbos.Api.Application.AiGatewayCredentialPool>();
builder.Services.AddSingleton<Rbos.Api.Application.IAiProviderAdapter, Rbos.Api.Application.GeminiAiProviderAdapter>();
builder.Services.AddSingleton<Rbos.Api.Application.IAiProviderAdapter, Rbos.Api.Application.GroqAiProviderAdapter>();
builder.Services.AddScoped<Rbos.Api.Application.IAiGatewayUsageLedger, Rbos.Api.Application.AiGatewayUsageLedger>();
builder.Services.AddScoped<Rbos.Api.Application.AiGateway>();
builder.Services.AddHttpClient<Rbos.Api.Application.InvoiceExtractionService>(h => h.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient<Rbos.Api.Application.GroqVisionClient>(h => h.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddHttpClient<Rbos.Api.Application.ItemTagCaptureService>(h => h.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddHttpClient<Rbos.Api.Application.SalesInvoiceCaptureService>(h => h.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddScoped<Rbos.Api.Application.VoiceNavigationIntentService>();
builder.Services.AddScoped<Rbos.Api.Application.ProductFileReader>();
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
    o.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
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
	o.ExpireTimeSpan = TimeSpan.FromDays(Math.Clamp(builder.Configuration.GetValue("Auth:PersistentCookieDays", 30), 1, 90));
    o.SlidingExpiration = true;
    o.Events.OnValidatePrincipal = async context =>
    {
        // Keep Identity's normal security-stamp validation. It may rebuild the
        // principal, so preserve this cookie's server-issued session ID afterward.
        var originalSessionId = context.Principal?.FindFirst(AuthSessionService.SessionClaimType)?.Value;
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        var principal = context.Principal;
        if (principal?.Identity?.IsAuthenticated != true) return;

        var hasSessionId = Guid.TryParse(originalSessionId, out var sessionId);
        if (hasSessionId && principal.Identity is ClaimsIdentity identity &&
            identity.FindFirst(AuthSessionService.SessionClaimType) is null)
        {
            identity.AddClaim(new Claim(AuthSessionService.SessionClaimType, sessionId.ToString("D")));
        }

        var hasUserId = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        var sessions = context.HttpContext.RequestServices.GetRequiredService<AuthSessionService>();
        if (!hasSessionId || !hasUserId ||
            !await sessions.IsActiveAsync(sessionId, userId, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
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
var staticContentTypes = new FileExtensionContentTypeProvider();
staticContentTypes.Mappings[".cact"] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticContentTypes });
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/api/health").AllowAnonymous();
Rbos.Api.Endpoints.Map(app);
Rbos.Api.WhatsAppWebhookEndpoints.Map(app);
Func<IConfiguration, IResult> assetLinksHandler = configuration =>
{
    var configured = configuration["Android:AssetLinksJson"];
    if (string.IsNullOrWhiteSpace(configured))
        return Results.Json(Array.Empty<object>());
    try
    {
        using var document = System.Text.Json.JsonDocument.Parse(configured);
        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            return Results.Problem("Android asset links configuration must be a JSON array.", statusCode: 500);
        return Results.Content(document.RootElement.GetRawText(), "application/json");
    }
    catch (System.Text.Json.JsonException)
    {
        return Results.Problem("Android asset links configuration is not valid JSON.", statusCode: 500);
    }
};

// Android App Links requires the root well-known URL. Keep the API-prefixed alias
// as well because the integration-test API client resolves Raw() paths under /api.
app.MapGet("/.well-known/assetlinks.json", assetLinksHandler).AllowAnonymous();
app.MapGet("/api/.well-known/assetlinks.json", assetLinksHandler).AllowAnonymous();
app.MapFallback("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();
app.Run();

public partial class Program { }

