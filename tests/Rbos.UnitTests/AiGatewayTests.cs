using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public sealed class AiGatewayTests
{
    [Fact]
    public void CredentialPool_RotatesKeys_And429CoolsEveryKeyInSameProject()
    {
        var config = Config(
            ("AiGateway:Enabled", "true"),
            ("AiGateway:Providers:Gemini:Enabled", "true"),
            ("AiGateway:Credentials:0:Provider", "Gemini"),
            ("AiGateway:Credentials:0:Id", "gemini-a"),
            ("AiGateway:Credentials:0:SecretRef", "TestKeys:A"),
            ("AiGateway:Credentials:0:ProjectId", "project-one"),
            ("AiGateway:Credentials:0:BillingMode", "Free"),
            ("AiGateway:Credentials:1:Provider", "Gemini"),
            ("AiGateway:Credentials:1:Id", "gemini-b"),
            ("AiGateway:Credentials:1:SecretRef", "TestKeys:B"),
            ("AiGateway:Credentials:1:ProjectId", "project-one"),
            ("AiGateway:Credentials:1:BillingMode", "Free"),
            ("TestKeys:A", "secret-a"),
            ("TestKeys:B", "secret-b"));
        var pool = new AiGatewayCredentialPool(config);

        var first = pool.SelectOrder(AiProviderKind.Gemini, AiCapability.Text, "model", false);
        var second = pool.SelectOrder(AiProviderKind.Gemini, AiCapability.Text, "model", false);

        Assert.Equal(2, first.Count);
        Assert.NotEqual(first[0].Id, second[0].Id);
        Assert.Equal(first[0].QuotaBucketId, first[1].QuotaBucketId);
        pool.MarkRateLimited(first[0], TimeSpan.FromMinutes(2));
        Assert.Empty(pool.SelectOrder(AiProviderKind.Gemini, AiCapability.Text, "model", false));
    }

    [Fact]
    public void CredentialPool_RoundRobinRemainsBalancedUnderConcurrentRequests()
    {
        var config = Config(
            ("AiGateway:Providers:Gemini:Enabled", "true"),
            ("AiGateway:Credentials:0:Provider", "Gemini"),
            ("AiGateway:Credentials:0:Id", "key-a"),
            ("AiGateway:Credentials:0:SecretRef", "TestKeys:A"),
            ("AiGateway:Credentials:0:ProjectId", "project-one"),
            ("AiGateway:Credentials:0:BillingMode", "Free"),
            ("AiGateway:Credentials:1:Provider", "Gemini"),
            ("AiGateway:Credentials:1:Id", "key-b"),
            ("AiGateway:Credentials:1:SecretRef", "TestKeys:B"),
            ("AiGateway:Credentials:1:ProjectId", "project-two"),
            ("AiGateway:Credentials:1:BillingMode", "Free"),
            ("TestKeys:A", "secret-a"),
            ("TestKeys:B", "secret-b"));
        var pool = new AiGatewayCredentialPool(config);
        var selections = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 100, _ =>
            selections.Add(pool.SelectOrder(AiProviderKind.Gemini, AiCapability.Text, "model", false)[0].Id));

        var counts = selections.GroupBy(x => x).Select(x => x.Count()).OrderBy(x => x).ToArray();
        Assert.Equal(2, counts.Length);
        Assert.InRange(counts[1] - counts[0], 0, 1);
    }

    [Fact]
    public void CredentialPool_RateLimitSkipsOnlyTheAffectedIndependentProject()
    {
        var config = Config(
            ("AiGateway:Providers:Gemini:Enabled", "true"),
            ("AiGateway:Credentials:0:Provider", "Gemini"),
            ("AiGateway:Credentials:0:Id", "project-a-key"),
            ("AiGateway:Credentials:0:SecretRef", "TestKeys:A"),
            ("AiGateway:Credentials:0:ProjectId", "project-a"),
            ("AiGateway:Credentials:0:BillingMode", "Free"),
            ("AiGateway:Credentials:1:Provider", "Gemini"),
            ("AiGateway:Credentials:1:Id", "project-b-key"),
            ("AiGateway:Credentials:1:SecretRef", "TestKeys:B"),
            ("AiGateway:Credentials:1:ProjectId", "project-b"),
            ("AiGateway:Credentials:1:BillingMode", "Free"),
            ("TestKeys:A", "secret-a"),
            ("TestKeys:B", "secret-b"));
        var pool = new AiGatewayCredentialPool(config);
        var order = pool.SelectOrder(AiProviderKind.Gemini, AiCapability.Text, "model", false);

        pool.MarkRateLimited(order[0], TimeSpan.FromMinutes(1));

        var remaining = pool.SelectOrder(AiProviderKind.Gemini, AiCapability.Text, "model", false);
        Assert.Single(remaining);
        Assert.Equal(order[1].QuotaBucketId, remaining[0].QuotaBucketId);
    }

    [Fact]
    public void CredentialPool_RotatesAcrossFiveConfiguredGroqTeamsWithoutCodeChanges()
    {
        var values = new List<(string Key, string Value)>
        {
            ("AiGateway:Providers:Groq:Enabled", "true")
        };
        for (var i = 0; i < 5; i++)
        {
            values.Add(($"AiGateway:Credentials:{i}:Provider", "Groq"));
            values.Add(($"AiGateway:Credentials:{i}:Id", $"groq-team-{i + 1}-key"));
            values.Add(($"AiGateway:Credentials:{i}:SecretRef", $"TestKeys:Groq{i + 1}"));
            values.Add(($"AiGateway:Credentials:{i}:TeamId", $"team-{i + 1}"));
            values.Add(($"AiGateway:Credentials:{i}:BillingMode", "Free"));
            values.Add(($"TestKeys:Groq{i + 1}", $"secret-{i + 1}"));
        }

        var pool = new AiGatewayCredentialPool(Config(values.ToArray()));
        var selected = Enumerable.Range(0, 5)
            .Select(_ => pool.SelectOrder(AiProviderKind.Groq, AiCapability.Text, "model", allowPaid: false)[0])
            .ToArray();

        Assert.Equal(5, selected.Select(x => x.Id).Distinct().Count());
        Assert.Equal(5, selected.Select(x => x.QuotaBucketId).Distinct().Count());

        pool.MarkRateLimited(selected[0], TimeSpan.FromMinutes(1));
        var remaining = pool.SelectOrder(AiProviderKind.Groq, AiCapability.Text, "model", allowPaid: false);
        Assert.Equal(4, remaining.Count);
        Assert.DoesNotContain(remaining, x => x.QuotaBucketId == selected[0].QuotaBucketId);
    }
    [Fact]
    public void CredentialPool_UnknownBillingModeIsFailClosed()
    {
        var config = Config(
            ("AiGateway:Providers:Gemini:Enabled", "true"),
            ("Gemini:ApiKey", "secret"),
            ("Gemini:ProjectId", "project-one"));
        var pool = new AiGatewayCredentialPool(config);

        Assert.Empty(pool.SelectOrder(AiProviderKind.Gemini, AiCapability.Text, "model", allowPaid: false));
    }

    [Fact]
    public void CredentialPool_PaidCredentialRequiresExplicitOptInAndPricing()
    {
        var config = Config(
            ("AiGateway:Providers:Gemini:Enabled", "true"),
            ("AiGateway:Credentials:0:Provider", "Gemini"),
            ("AiGateway:Credentials:0:Id", "paid-key"),
            ("AiGateway:Credentials:0:SecretRef", "TestKeys:Paid"),
            ("AiGateway:Credentials:0:ProjectId", "project-paid"),
            ("AiGateway:Credentials:0:BillingMode", "Paid"),
            ("AiGateway:Credentials:0:InputCostPerMillionInr", "10"),
            ("AiGateway:Credentials:0:OutputCostPerMillionInr", "10"),
            ("TestKeys:Paid", "secret"));
        var pool = new AiGatewayCredentialPool(config);

        Assert.Empty(pool.SelectOrder(AiProviderKind.Gemini, AiCapability.Text, "model", allowPaid: false));
        Assert.Single(pool.SelectOrder(AiProviderKind.Gemini, AiCapability.Text, "model", allowPaid: true));
    }

    [Fact]
    public void PaidCredential_DoesNotRunWhenInputOrOutputPricingIsIncomplete()
    {
        var credential = new AiProviderCredential("paid", AiProviderKind.Gemini, "project-a", "secret",
            AiBillingMode.Paid, Enum.GetValues<AiCapability>().ToHashSet(), [], 10m, null, null, null);
        var request = Request();

        Assert.True(credential.HasPaidPricing);
        Assert.False(credential.CanEstimatePaidCost(request));
    }

    [Fact]
    public async Task Gateway_BlocksProviderCallWhenBudgetReservationIsDenied()
    {
        var config = LegacyGeminiConfig();
        var pool = new AiGatewayCredentialPool(config);
        var adapter = new FakeAdapter(AiProviderKind.Gemini, (_, _, _, _) =>
            Task.FromResult(new AiProviderResponse(null, "should not run", 1, 1)));
        var ledger = new FakeLedger(allowed: false);
        var gateway = new AiGateway(config, pool, [adapter], ledger, NullLogger<AiGateway>.Instance);

        var response = await gateway.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Null(response);
        Assert.Equal(1, ledger.ReserveCalls);
        Assert.Equal(0, adapter.Calls);
    }

    [Fact]
    public async Task Gateway_SkipsPaidProviderAfterBudgetStopAndContinuesToFreeProvider()
    {
        var config = Config(
            ("AiGateway:Enabled", "true"),
            ("AiGateway:AllowPaidProviders", "true"),
            ("AiGateway:Providers:Gemini:Enabled", "true"),
            ("AiGateway:Providers:Groq:Enabled", "true"),
            ("AiGateway:Credentials:0:Provider", "Gemini"),
            ("AiGateway:Credentials:0:Id", "paid-gemini"),
            ("AiGateway:Credentials:0:SecretRef", "TestKeys:Paid"),
            ("AiGateway:Credentials:0:ProjectId", "paid-project"),
            ("AiGateway:Credentials:0:BillingMode", "Paid"),
            ("AiGateway:Credentials:0:InputCostPerMillionInr", "10"),
            ("AiGateway:Credentials:0:OutputCostPerMillionInr", "10"),
            ("AiGateway:Credentials:1:Provider", "Groq"),
            ("AiGateway:Credentials:1:Id", "free-groq"),
            ("AiGateway:Credentials:1:SecretRef", "TestKeys:Free"),
            ("AiGateway:Credentials:1:TeamId", "free-team"),
            ("AiGateway:Credentials:1:BillingMode", "Free"),
            ("TestKeys:Paid", "paid-secret"),
            ("TestKeys:Free", "free-secret"));
        var pool = new AiGatewayCredentialPool(config);
        var gemini = new FakeAdapter(AiProviderKind.Gemini, (_, _, _, _) =>
            Task.FromResult(new AiProviderResponse(null, "paid", 1, 1)));
        var groq = new FakeAdapter(AiProviderKind.Groq, (_, _, _, _) =>
            Task.FromResult(new AiProviderResponse(null, "free", 1, 1)));
        var ledger = new FakeLedger(credential => credential.BillingMode == AiBillingMode.Free);
        var gateway = new AiGateway(config, pool, [gemini, groq], ledger, NullLogger<AiGateway>.Instance);

        var response = await gateway.ExecuteAsync(Request(AiProviderKind.Gemini, AiProviderKind.Groq), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("Groq", response.Provider);
        Assert.Equal("free", response.Text);
        Assert.Equal(0, gemini.Calls);
        Assert.Equal(1, groq.Calls);
    }

    [Fact]
    public async Task Gateway_RateLimitFallsBackToAnotherConfiguredProvider()
    {
        var config = LegacyGeminiAndGroqConfig();
        var pool = new AiGatewayCredentialPool(config);
        var gemini = new FakeAdapter(AiProviderKind.Gemini, (_, _, _, _) =>
            throw new AiProviderException(AiFailureCategory.RateLimited, "rate limited", HttpStatusCode.TooManyRequests));
        var groq = new FakeAdapter(AiProviderKind.Groq, (_, _, _, _) =>
            Task.FromResult(new AiProviderResponse(null, "fallback", 4, 2)));
        var ledger = new FakeLedger(allowed: true);
        var gateway = new AiGateway(config, pool, [gemini, groq], ledger, NullLogger<AiGateway>.Instance);

        var response = await gateway.ExecuteAsync(Request(AiProviderKind.Gemini, AiProviderKind.Groq), CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("Groq", response.Provider);
        Assert.Equal("fallback", response.Text);
        Assert.Equal(1, gemini.Calls);
        Assert.Equal(1, groq.Calls);
        Assert.Equal(2, ledger.ReserveCalls);
        Assert.Equal(2, ledger.CompleteCalls);
    }

    [Fact]
    public async Task Gateway_ContentSafetyRefusalDoesNotFallThroughToAnotherProvider()
    {
        var config = LegacyGeminiAndGroqConfig();
        var pool = new AiGatewayCredentialPool(config);
        var gemini = new FakeAdapter(AiProviderKind.Gemini, (_, _, _, _) =>
            throw new AiProviderException(AiFailureCategory.ContentSafetyRefusal, "provider refusal"));
        var groq = new FakeAdapter(AiProviderKind.Groq, (_, _, _, _) =>
            Task.FromResult(new AiProviderResponse(null, "must not run", 1, 1)));
        var ledger = new FakeLedger(allowed: true);
        var gateway = new AiGateway(config, pool, [gemini, groq], ledger, NullLogger<AiGateway>.Instance);

        await Assert.ThrowsAsync<AiProviderException>(() =>
            gateway.ExecuteAsync(Request(AiProviderKind.Gemini, AiProviderKind.Groq), CancellationToken.None));

        Assert.Equal(1, gemini.Calls);
        Assert.Equal(0, groq.Calls);
        Assert.Equal(1, ledger.CompleteCalls);
    }

    [Fact]
    public void StructuredOutputValidator_EnforcesRequiredFieldsAndDeclaredTypes()
    {
        var schema = new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "OBJECT",
            ["required"] = new System.Text.Json.Nodes.JsonArray(System.Text.Json.Nodes.JsonValue.Create("name")),
            ["properties"] = new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "STRING" },
                ["amount"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "NUMBER" }
            }
        };

        Assert.True(AiStructuredOutputValidator.IsValid(
            System.Text.Json.Nodes.JsonNode.Parse("{\"name\":\"Cable\"}")!, schema));
        Assert.True(AiStructuredOutputValidator.IsValid(
            System.Text.Json.Nodes.JsonNode.Parse("{\"name\":\"Cable\",\"amount\":\"1250.50\"}")!, schema));
        Assert.False(AiStructuredOutputValidator.IsValid(
            System.Text.Json.Nodes.JsonNode.Parse("{\"name\":\"Cable\",\"amount\":\"not-a-number\"}")!, schema));
        Assert.False(AiStructuredOutputValidator.IsValid(
            System.Text.Json.Nodes.JsonNode.Parse("{\"name\":42}")!, schema));
        Assert.False(AiStructuredOutputValidator.IsValid(
            System.Text.Json.Nodes.JsonNode.Parse("{}")!, schema));
    }

    internal static IConfiguration LegacyGeminiConfig() => Config(
        ("AiGateway:Enabled", "true"),
        ("AiGateway:AllowPaidProviders", "false"),
        ("AiGateway:Providers:Gemini:Enabled", "true"),
        ("AiGateway:LegacyGeminiBillingMode", "Free"),
        ("Gemini:ApiKey", "gemini-test-secret"),
        ("Gemini:ProjectId", "gemini-test-project"));

    internal static IConfiguration LegacyGeminiAndGroqConfig() => Config(
        ("AiGateway:Enabled", "true"),
        ("AiGateway:AllowPaidProviders", "false"),
        ("AiGateway:Providers:Gemini:Enabled", "true"),
        ("AiGateway:Providers:Groq:Enabled", "true"),
        ("AiGateway:LegacyGeminiBillingMode", "Free"),
        ("AiGateway:LegacyGroqBillingMode", "Free"),
        ("Gemini:ApiKey", "gemini-test-secret"),
        ("Gemini:ProjectId", "gemini-test-project"),
        ("Groq:ApiKey", "groq-test-secret"),
        ("Groq:TeamId", "groq-test-team"));

    internal static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(x => x.Key, x => (string?)x.Value)).Build();

    internal static AiGatewayRequest Request(params AiProviderKind[] providers) => new()
    {
        TenantId = Guid.NewGuid(),
        ActorUserId = Guid.NewGuid(),
        Feature = "TestFeature",
        Task = AiTaskKind.TextGeneration,
        Prompt = "Say hello",
        ProviderOrder = providers.Length == 0 ? [AiProviderKind.Gemini] : providers
    };

    internal sealed class FakeAdapter(
        AiProviderKind provider,
        Func<AiGatewayRequest, AiProviderCredential, string, CancellationToken, Task<AiProviderResponse>> execute) : IAiProviderAdapter
    {
        public AiProviderKind Provider => provider;
        public int Calls { get; private set; }
        public bool Supports(AiCapability capability) => true;

        public Task<AiProviderResponse> ExecuteAsync(
            AiGatewayRequest request, AiProviderCredential credential, string model, CancellationToken cancellationToken)
        {
            Calls++;
            return execute(request, credential, model, cancellationToken);
        }
    }

    internal sealed class FakeLedger : IAiGatewayUsageLedger
    {
        private readonly Func<AiProviderCredential, bool> _allow;
        public FakeLedger(bool allowed) : this(_ => allowed) { }
        public FakeLedger(Func<AiProviderCredential, bool> allow) => _allow = allow;
        public int ReserveCalls { get; private set; }
        public int CompleteCalls { get; private set; }

        public Task<AiBudgetReservation> ReserveAsync(
            AiGatewayRequest request, AiProviderCredential credential, string model, decimal estimatedCost, CancellationToken ct)
        {
            ReserveCalls++;
            var allowed = _allow(credential);
            var reason = allowed ? null : credential.BillingMode == AiBillingMode.Paid ? "monthly_budget_exceeded" : "test-budget-limit";
            return Task.FromResult(new AiBudgetReservation(Guid.NewGuid(), allowed, 0, 0, false, reason));
        }

        public Task CompleteAsync(
            AiGatewayRequest request, Guid reservationId, string outcome, int? inputTokens, int? outputTokens,
            decimal? actualCost, int latencyMilliseconds, string? errorCategory, int retryCount, CancellationToken ct)
        {
            CompleteCalls++;
            return Task.CompletedTask;
        }
    }
}
