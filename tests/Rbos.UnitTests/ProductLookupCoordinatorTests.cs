using Microsoft.Extensions.Logging.Abstractions;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public class ProductLookupCoordinatorTests
{
    sealed class StubProvider(string name, Func<string, ProviderLookupResult> fn) : IProductLookupProvider
    {
        public string Name => name;
        public bool Enabled => true;
        public int Calls;
        public Task<ProviderLookupResult> LookupAsync(string barcode, CancellationToken ct) { Calls++; return Task.FromResult(fn(barcode)); }
    }

    sealed class StubCache : ExternalBarcodeLookupCache
    {
        readonly Dictionary<string, ProviderLookupResult> map = new();
        public StubCache() : base(null!) { }
        public void Seed(string provider, ProviderLookupResult r) => map[provider] = r;
        public override Task<ProviderLookupResult?> GetAsync(Guid uid, string barcode, string provider, CancellationToken ct)
            => Task.FromResult(map.TryGetValue(provider, out var r) ? r : null);
        public override Task UpsertAsync(Guid uid, string barcode, ProviderLookupResult result, CancellationToken ct)
        { map[result.Provider] = result; return Task.CompletedTask; }
    }

    [Fact]
    public async Task Coordinator_returns_found_and_notfound_statuses()
    {
        var p1 = new StubProvider("P1", _ => new ProviderLookupResult("P1", ExternalLookupStatus.NotFound, Message: "miss"));
        var p2 = new StubProvider("P2", _ => new ProviderLookupResult("P2", ExternalLookupStatus.Found,
            new ExternalProductInfo("P2", "LG TV", "LG", "M1", null, null, "attr", DateTimeOffset.UtcNow)));
        var cache = new StubCache();
        var c = new ProductLookupCoordinator([p1, p2], cache, NullLogger<ProductLookupCoordinator>.Instance);
        var (results, statuses) = await c.LookupAsync(Guid.NewGuid(), "8806087972538", CancellationToken.None);
        Assert.Single(results);
        Assert.Equal(2, statuses.Count);
        Assert.Contains(statuses, x => x.Provider == "P1" && x.Status == ExternalLookupStatus.NotFound);
        Assert.Contains(statuses, x => x.Provider == "P2" && x.Status == ExternalLookupStatus.Found);
    }

    [Fact]
    public async Task Coordinator_uses_cache_and_avoids_provider_call_for_known_notfound()
    {
        var p1 = new StubProvider("P1", _ => new ProviderLookupResult("P1", ExternalLookupStatus.Found,
            new ExternalProductInfo("P1", "ShouldNotCall", null, null, null, null)));
        var cache = new StubCache();
        cache.Seed("P1", new ProviderLookupResult("P1", ExternalLookupStatus.NotFound, Message: "cached miss"));
        var c = new ProductLookupCoordinator([p1], cache, NullLogger<ProductLookupCoordinator>.Instance);
        var (results, statuses) = await c.LookupAsync(Guid.NewGuid(), "12345670", CancellationToken.None);
        Assert.Empty(results);
        Assert.Single(statuses);
        Assert.Equal(ExternalLookupStatus.NotFound, statuses[0].Status);
        Assert.Equal(0, p1.Calls);
    }

    [Fact]
    public async Task Coordinator_surfaces_timeout_and_unavailable_statuses()
    {
        var p1 = new StubProvider("TimeoutProvider", _ => new ProviderLookupResult("TimeoutProvider", ExternalLookupStatus.Timeout, Message: "Provider timeout."));
        var p2 = new StubProvider("UnavailableProvider", _ => new ProviderLookupResult("UnavailableProvider", ExternalLookupStatus.Unavailable, Message: "Provider unavailable."));
        var c = new ProductLookupCoordinator([p1, p2], new StubCache(), NullLogger<ProductLookupCoordinator>.Instance);
        var (results, statuses) = await c.LookupAsync(Guid.NewGuid(), "12345670", CancellationToken.None);
        Assert.Empty(results);
        Assert.Equal(2, statuses.Count);
        Assert.Contains(statuses, s => s.Status == ExternalLookupStatus.Timeout);
        Assert.Contains(statuses, s => s.Status == ExternalLookupStatus.Unavailable);
    }
}
