using System.Security.Claims;
using Rbos.Api.Application;

namespace Rbos.Api;

public sealed record ProductUpsertRequest(string? Name, string? CategoryName, bool IsGeneric, string? BaseUnit, string? Brand, string? Model, Dictionary<string, string>? Attributes, bool? IsActive);
public sealed record ImportRowRequest(string? Name, string? CategoryName, bool IsGeneric, string? BaseUnit, string? Brand, string? Model, Dictionary<string, string>? Attributes);
public sealed record ImportRequest(List<ImportRowRequest>? Rows);
public sealed record RenameCategoryRequest(string? Name);
public sealed record ImportFile(string? FileName, string? ContentType, string? Data);

public static class CatalogueEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/products", async (string? q, Guid? categoryId, bool? includeInactive, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
            => Reply(await svc.SearchAsync(Uid(p), q, categoryId, includeInactive ?? false, ct))).RequireAuthorization();

        api.MapPost("/products", async (ProductUpsertRequest m, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
            => Reply(await svc.CreateAsync(Uid(p), ToCommand(m), ct))).RequireAuthorization();

        api.MapPut("/products/{id:guid}", async (Guid id, ProductUpsertRequest m, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
            => Reply(await svc.UpdateAsync(Uid(p), id, ToCommand(m), ct))).RequireAuthorization();

        api.MapGet("/categories", async (ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
            => Reply(await svc.CategoriesAsync(Uid(p), ct))).RequireAuthorization();

        api.MapPut("/categories/{id:guid}", async (Guid id, RenameCategoryRequest m, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
            => Reply(await svc.RenameCategoryAsync(Uid(p), id, m.Name, ct))).RequireAuthorization();

        api.MapPost("/products/import/read", async (ImportFile m, ProductFileReader r, CancellationToken ct)
            => Reply(await r.ReadAsync(m.FileName, m.ContentType, m.Data, ct))).RequireAuthorization();

        api.MapPost("/products/import/preview", async (ImportRequest m, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
            => Reply(await svc.PreviewImportAsync(Uid(p), ToRows(m), ct))).RequireAuthorization();

        api.MapPost("/products/import/commit", async (ImportRequest m, HttpRequest req, ClaimsPrincipal p, CatalogueService svc, CancellationToken ct)
            => Reply(await svc.CommitImportAsync(Uid(p), req.Headers["Idempotency-Key"].ToString(), ToRows(m), ct))).RequireAuthorization();
    }

    static Guid Uid(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);

    static ProductCommand ToCommand(ProductUpsertRequest m) => new(m.Name, m.CategoryName, m.IsGeneric, m.BaseUnit, m.Brand, m.Model, m.Attributes, m.IsActive ?? true);

    static List<ImportRowInput>? ToRows(ImportRequest m) =>
        m.Rows?.Select(r => new ImportRowInput(r.Name, r.CategoryName, r.IsGeneric, r.BaseUnit, r.Brand, r.Model, r.Attributes)).ToList();

    static IResult Reply<T>(ServiceResult<T> r) => r.Error is null
        ? Results.Ok(r.Value)
        : r.Status == 409 ? Results.Conflict(new { error = r.Error })
        : r.Status == 404 ? Results.NotFound(new { error = r.Error })
        : Results.BadRequest(new { error = r.Error });
}
