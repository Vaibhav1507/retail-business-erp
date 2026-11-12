using System.Security.Claims;
using Rbos.Api.Application;

using static Rbos.Api.ApiResults;

namespace Rbos.Api;

public sealed record ProductUpsertRequest(string? Name, string? CategoryName, bool IsGeneric, string? BaseUnit, string? Brand, string? Model, Dictionary<string, string>? Attributes, bool? IsActive, string? HsnCode = null, decimal? GstRate = null, decimal? Mrp = null, Dictionary<string, string>? Specs = null);
public sealed record ImportRowRequest(string? Name, string? CategoryName, bool IsGeneric, string? BaseUnit, string? Brand, string? Model, Dictionary<string, string>? Attributes, string? Barcode = null, decimal? Mrp = null, Dictionary<string, string>? Specs = null, int? SourcePage = null);
public sealed record ImportRequest(List<ImportRowRequest>? Rows, ImportSource? Source = null);
public sealed record RenameCategoryRequest(string? Name);
public sealed record ImportFile(string? FileName, string? ContentType, string? Data);

public static class CatalogueEndpoints
{
	public static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/products", async (string? q, Guid? categoryId, bool? includeInactive, Guid? brandId, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
			=> Reply(await svc.SearchAsync(Uid(p), q, categoryId, includeInactive ?? false, ct, brandId))).RequireAuthorization();

		api.MapPost("/products", async (ProductUpsertRequest m, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
			=> Reply(await svc.CreateAsync(Uid(p), ToCommand(m), ct))).RequireAuthorization();

		api.MapPut("/products/{id:guid}", async (Guid id, ProductUpsertRequest m, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
			=> Reply(await svc.UpdateAsync(Uid(p), id, ToCommand(m), ct))).RequireAuthorization();

		api.MapGet("/categories/{id:guid}/attributes", async (Guid id, ClaimsPrincipal p, CategoryAttributeService svc, CancellationToken ct)
			=> Reply(await svc.GetAsync(Uid(p), id, ct))).RequireAuthorization();

		api.MapPut("/categories/{id:guid}/attributes", async (Guid id, List<CategoryAttributeDefinition> definitions, ClaimsPrincipal p, CategoryAttributeService svc, CancellationToken ct)
			=> Reply(await svc.SaveAsync(Uid(p), id, definitions, ct))).RequireAuthorization();

		api.MapGet("/categories", async (ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
			=> Reply(await svc.CategoriesAsync(Uid(p), ct))).RequireAuthorization();

		api.MapPut("/categories/{id:guid}", async (Guid id, RenameCategoryRequest m, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
			=> Reply(await svc.RenameCategoryAsync(Uid(p), id, m.Name, ct))).RequireAuthorization();

		api.MapPost("/products/import/read", async (ImportFile m, ClaimsPrincipal p, ProductFileReader r, CategoryAttributeService attributes, CancellationToken ct) =>
		{
			var guidance = await attributes.PromptGuidanceAsync(Uid(p), ct);
			return Reply(await r.ReadAsync(m.FileName, m.ContentType, m.Data, guidance, ct));
		}).RequireAuthorization();

		api.MapPost("/products/import/preview", async (ImportRequest m, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
			=> Reply(await svc.PreviewImportAsync(Uid(p), ToRows(m), ct))).RequireAuthorization();

		api.MapPost("/products/import/commit", async (ImportRequest m, HttpRequest req, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
			=> Reply(await svc.CommitImportAsync(Uid(p), req.Headers["Idempotency-Key"].ToString(), ToRows(m), ct, m.Source))).RequireAuthorization();

		api.MapGet("/products/suggest-for-barcode", async (string? code, string? model, Guid? brandId, string? text, ClaimsPrincipal p, BarcodeService svc, ProductLookupCoordinator lookup, CancellationToken ct) =>
		{
			if (!string.IsNullOrWhiteSpace(code))
			{
				var localHit = await svc.LookupAsync(Uid(p), code, ct);
				if (localHit.Error is null)
				{
					var localOnly = await svc.SuggestAsync(Uid(p), code, model, brandId, text, ct);
					if (localOnly.Error is not null) return Reply(localOnly);
					return Reply(ServiceResult<object>.Ok(new
					{
						local = localOnly.Value,
						external = new List<ExternalProductInfo>(),
						providerResults = new List<ProviderLookupResult>(),
						internalBarcodeMatch = true
					}));
				}
			}
			var local = await svc.SuggestAsync(Uid(p), code, model, brandId, text, ct);
			if (local.Error is not null) return Reply(local);
			var (ext, providerResults) = string.IsNullOrWhiteSpace(code)
				? (new List<ExternalProductInfo>(), new List<ProviderLookupResult>())
				: await lookup.LookupAsync(Uid(p), code.Trim(), ct);
			return Reply(ServiceResult<object>.Ok(new { local = local.Value, external = ext, providerResults, internalBarcodeMatch = false }));
		}).RequireAuthorization();
		api.MapGet("/catalogue/source-documents", async (ClaimsPrincipal p, CatalogueService svc, CancellationToken ct) => Reply(await svc.SourceDocumentsAsync(Uid(p), ct))).RequireAuthorization();
		api.MapGet("/catalogue-sources", () => Results.Ok(Rbos.Domain.ManufacturerSources.All)).RequireAuthorization();
	}

	static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);

	static ProductCommand ToCommand(ProductUpsertRequest m) => new(m.Name, m.CategoryName, m.IsGeneric, m.BaseUnit, m.Brand, m.Model, m.Attributes, m.IsActive ?? true, m.HsnCode, m.GstRate, m.Mrp, m.Specs);

	static List<ImportRowInput>? ToRows(ImportRequest m) =>
		m.Rows?.Select(r => new ImportRowInput(r.Name, r.CategoryName, r.IsGeneric, r.BaseUnit, r.Brand, r.Model, r.Attributes, r.Barcode, r.Mrp, r.Specs, r.SourcePage)).ToList();
}
