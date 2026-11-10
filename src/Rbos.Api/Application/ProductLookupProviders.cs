namespace Rbos.Api.Application;

/// <summary>Reference data a provider may suggest. Never carries price, GST, HSN or stock; callers must not apply those from providers.</summary>
public sealed record ExternalProductInfo(string Source, string? Name, string? Brand, string? Model, string? Description, string? ImageUrl,
	string? Attribution = null, DateTimeOffset? LookedUpAtUtc = null);

public enum ExternalLookupStatus { Found, NotFound, Timeout, RateLimited, Unavailable, Error }

public sealed record ProviderLookupResult(string Provider, ExternalLookupStatus Status, ExternalProductInfo? Product = null,
	string? Message = null, DateTimeOffset? LookedUpAtUtc = null);

public interface IProductLookupProvider
{
	string Name { get; }
	bool Enabled { get; }
	Task<ProviderLookupResult> LookupAsync(string barcode, CancellationToken ct);
}

/// <summary>
/// Open Food Facts (free, no key, ODbL licence: attribution + share-alike on derived databases).
/// Food-focused; coverage of Indian and non-food products is limited, so a 404 is a normal "not found".
/// Disable with Providers:OpenFoodFacts:Enabled=false. Only the barcode is sent. Anonymous read use only.
/// </summary>
public sealed class OpenFoodFactsProvider(HttpClient http, IConfiguration cfg) : IProductLookupProvider
{
	public string Name => "Open Food Facts";
	public bool Enabled => cfg.GetValue("Providers:OpenFoodFacts:Enabled", true);

	public async Task<ProviderLookupResult> LookupAsync(string barcode, CancellationToken ct)
	{
		var now = DateTimeOffset.UtcNow;
		if (barcode.Length is < 8 or > 14 || !barcode.All(char.IsAsciiDigit))
			return new ProviderLookupResult(Name, ExternalLookupStatus.NotFound, Message: "Barcode format not supported by this provider.", LookedUpAtUtc: now);
		try
		{
			using var req = new HttpRequestMessage(HttpMethod.Get,
				$"https://world.openfoodfacts.org/api/v2/product/{barcode}.json?fields=code,product_name,brands,generic_name,image_front_small_url");
			req.Headers.UserAgent.ParseAdd("RBOS/1.0 (retail back-office; barcode lookup)");
			using var res = await http.SendAsync(req, ct);
			if ((int)res.StatusCode == 429)
				return new ProviderLookupResult(Name, ExternalLookupStatus.RateLimited, Message: "Provider rate limit reached.", LookedUpAtUtc: now);
			if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
				return new ProviderLookupResult(Name, ExternalLookupStatus.NotFound, Message: "Barcode not found.", LookedUpAtUtc: now);
			res.EnsureSuccessStatusCode();
			using var doc = await System.Text.Json.JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
			if (!doc.RootElement.TryGetProperty("product", out var p))
				return new ProviderLookupResult(Name, ExternalLookupStatus.NotFound, Message: "No product payload.", LookedUpAtUtc: now);
			string? S(string k) => p.TryGetProperty(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;
			var name = S("product_name");
			if (name is null)
				return new ProviderLookupResult(Name, ExternalLookupStatus.NotFound, Message: "No product name in provider response.", LookedUpAtUtc: now);
			var brand = S("brands")?.Split(',')[0].Trim();
			return new ProviderLookupResult(Name, ExternalLookupStatus.Found,
				new ExternalProductInfo("Open Food Facts (ODbL)", name, brand, null, S("generic_name"), S("image_front_small_url"),
					"Data © Open Food Facts contributors, ODbL", now), LookedUpAtUtc: now);
		}
		catch (TaskCanceledException) when (!ct.IsCancellationRequested)
		{
			return new ProviderLookupResult(Name, ExternalLookupStatus.Timeout, Message: "Provider timeout.", LookedUpAtUtc: now);
		}
		catch (HttpRequestException e)
		{
			return new ProviderLookupResult(Name, ExternalLookupStatus.Unavailable, Message: e.Message, LookedUpAtUtc: now);
		}
		catch (Exception e) when (e is not OperationCanceledException)
		{
			return new ProviderLookupResult(Name, ExternalLookupStatus.Error, Message: e.Message, LookedUpAtUtc: now);
		}
	}
}

/// <summary>Runs enabled providers in order; a failing or disabled provider never blocks local lookup.</summary>
public sealed class ProductLookupCoordinator(IEnumerable<IProductLookupProvider> providers, ExternalBarcodeLookupCache cache, ILogger<ProductLookupCoordinator> log)
{
	public async Task<(List<ExternalProductInfo> Results, List<ProviderLookupResult> ProviderResults)> LookupAsync(Guid uid, string barcode, CancellationToken ct)
	{
		var results = new List<ExternalProductInfo>();
		var providerResults = new List<ProviderLookupResult>();
		foreach (var p in providers)
		{
			if (!p.Enabled)
			{
				providerResults.Add(new ProviderLookupResult(p.Name, ExternalLookupStatus.Unavailable, Message: "Provider disabled.", LookedUpAtUtc: DateTimeOffset.UtcNow));
				continue;
			}
			try
			{
				var cached = await cache.GetAsync(uid, barcode, p.Name, ct);
				var r = cached ?? await p.LookupAsync(barcode, ct);
				if (cached is null) await cache.UpsertAsync(uid, barcode, r, ct);
				providerResults.Add(r);
				if (r.Status == ExternalLookupStatus.Found && r.Product is not null) results.Add(r.Product);
			}
			catch (Exception e) when (e is not OperationCanceledException)
			{
				log.LogWarning(e, "Lookup provider {Provider} failed", p.Name);
				providerResults.Add(new ProviderLookupResult(p.Name, ExternalLookupStatus.Unavailable, Message: "Provider unavailable.", LookedUpAtUtc: DateTimeOffset.UtcNow));
			}
		}
		return (results, providerResults);
	}
}
