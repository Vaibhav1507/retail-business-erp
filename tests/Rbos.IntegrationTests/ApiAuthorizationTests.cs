using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Rbos.Api;
using Rbos.Domain;

namespace Rbos.IntegrationTests;

public sealed class ApiAuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Every_registered_api_route_method_has_an_explicit_policy()
    {
        // Starting the host materialises the endpoint data sources.
        using var client = factory.CreateClient();

        var endpoints = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
            {
                var template = endpoint.RoutePattern.RawText;
                return template is not null &&
                    (template.Equals("/api", StringComparison.OrdinalIgnoreCase)
                     || template.StartsWith("/api/", StringComparison.OrdinalIgnoreCase));
            });

        var unclassified = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var template = endpoint.RoutePattern.RawText!;
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"];
            foreach (var method in methods)
            {
                if (ApiRouteAccess.Resolve(method, template) is null)
                    unclassified.Add($"{method} {template}");
            }
        }

        Assert.True(unclassified.Count == 0,
            "Every registered /api route and method must have an explicit access classification. Unclassified: "
            + string.Join(", ", unclassified.Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Unknown_paths_methods_and_prefix_lookalikes_are_not_classified()
    {
        Assert.Null(ApiRouteAccess.Resolve("GET", "/api/not-a-real-route"));
        Assert.Null(ApiRouteAccess.Resolve("PATCH", "/api/products"));
        Assert.Null(ApiRouteAccess.Resolve("GET", "/api/products-should-not-match"));
        Assert.Null(ApiRouteAccess.Resolve("POST", "/api/sales-not-a-route"));
    }

    [Fact]
    public void Existing_role_bundles_are_preserved_without_implicit_permission_widening()
    {
        Assert.All(Permissions.All, permission => Assert.True(Permissions.Has("Owner", permission), permission));
        Assert.True(Permissions.Has("Manager", Permissions.CatalogueManage));
        Assert.True(Permissions.Has("Manager", Permissions.StockAdjust));
        Assert.False(Permissions.Has("Manager", Permissions.FinanceManage));
        Assert.Equal(Permissions.StockAdjust, ApiRouteAccess.Resolve("POST", "/api/stock-adjustments")?.Permission);
        Assert.True(Permissions.Has("Accountant", Permissions.FinanceManage));
        Assert.False(Permissions.Has("Accountant", Permissions.SalesCreate));
        Assert.True(Permissions.Has("Cashier", Permissions.SalesCreate));
        Assert.False(Permissions.Has("Cashier", Permissions.CatalogueManage));
    }

    [Fact]
    public async Task Authenticated_user_without_an_active_business_can_bootstrap_but_cannot_call_member_routes()
    {
        var client = new Client(factory);
        await client.Register();

        Assert.Equal(HttpStatusCode.OK,
            (await client.Send(HttpMethod.Post, "/auth/login", new { email = client.Email, password = Client.Password })).StatusCode);

        // Identity-level routes intentionally work before the first business is created.
        Assert.Equal(HttpStatusCode.OK, (await client.Raw("/me")).StatusCode);

        // A signed-in account without an active membership is not an authenticated member.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.Raw("/products")).StatusCode);

        var bootstrap = await client.Send(
            HttpMethod.Post,
            "/bootstrap",
            new { businessName = "Authorization Test Shop", branchName = "Main", locationName = "Shop" },
            Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.Raw("/products")).StatusCode);
    }

    [Fact]
    public async Task Brand_endpoints_are_registered_and_owner_can_create_a_brand()
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();

        Assert.Equal(HttpStatusCode.OK, (await client.Raw("/brands")).StatusCode);

        var created = await client.Send(
            HttpMethod.Post,
            "/brands",
            new { name = "RBOS Test Brand", website = (string?)null, aliases = Array.Empty<string>(), isActive = true });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
    }
}
