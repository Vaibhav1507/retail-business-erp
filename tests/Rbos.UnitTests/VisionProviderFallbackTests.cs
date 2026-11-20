using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public sealed class VisionProviderFallbackTests
{
    [Fact]
    public void InvoiceGeminiModels_DefaultToStableExplicitModelIds()
    {
        var config = Config();

        Assert.Equal(
            new[] { "gemini-4-hydrogen", "gemini-3.8-flash", "gemini-3.5-flash-lite" },
            VisionProviderFallback.InvoiceGeminiModels(config));
    }

    [Fact]
    public void LegacyFloatingAndRetiredAliasesAreReplacedByStableDefaults()
    {
        var config = Config(
            ("Gemini:Model", "gemini-4-hydrogen"),
            ("Gemini:FallbackModel", "gemini-flash-latest"),
            ("Gemini:LastResortModel", "gemini-2.5-flash-lite"));

        Assert.Equal(
            new[] { "gemini-4-hydrogen", "gemini-3.8-flash", "gemini-3.5-flash-lite" },
            VisionProviderFallback.InvoiceGeminiModels(config));
    }

    [Fact]
    public void GroqInvoiceMerge_PreservesFirstIdentityAndLastUsableTotalsInPageOrder()
    {
        var first = JsonNode.Parse("""
            {"supplierName":"Acme Ltd","invoiceNumber":"INV-42","date":"2026-11-20","lines":[{"description":"Cable"}]}
            """)!;
        var second = JsonNode.Parse("""
            {"supplierName":"Wrong Supplier","invoiceNumber":"OTHER","date":"2026-11-21","total":1250,"lines":[{"description":"Charger"}]}
            """)!;
        var third = JsonNode.Parse("""
            {"supplierName":"","invoiceNumber":null,"date":null,"total":null,"subtotal":1000,"lines":[{"description":"Adapter"}]}
            """)!;

        var merged = GroqVisionClient.MergeInvoicePageResults(new JsonNode?[] { first, second, third });

        Assert.Equal("Acme Ltd", merged?["supplierName"]?.GetValue<string>());
        Assert.Equal("INV-42", merged?["invoiceNumber"]?.GetValue<string>());
        Assert.Equal("2026-11-20", merged?["date"]?.GetValue<string>());
        Assert.Equal(1250, merged?["total"]?.GetValue<int>());
        Assert.Equal(1000, merged?["subtotal"]?.GetValue<int>());
        Assert.Equal(3, (merged?["lines"] as JsonArray)?.Count);
    }

    [Fact]
    public async Task ExecuteAsync_UsesGroqBetweenPrimaryAndSecondaryGemini()
    {
        var order = new List<string>();
        var attempts = new[]
        {
            Attempt("Gemini", "gemini-4-hydrogen", _ =>
            {
                order.Add("Gemini-primary");
                throw new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable);
            }),
            Attempt("Groq", "qwen/qwen3.8-27b", _ =>
            {
                order.Add("Groq");
                return Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray(new JsonObject { ["description"] = "Cable" }) });
            }),
            Attempt("Gemini", "gemini-3.8-flash", _ =>
            {
                order.Add("Gemini-secondary");
                return Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray(new JsonObject { ["description"] = "Should not run" }) });
            })
        };

        var result = await VisionProviderFallback.ExecuteAsync(
            "SupplierInvoice", attempts, HasLines, NullLogger.Instance, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(new[] { "Gemini-primary", "Groq" }, order);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsRemainingGeminiModelsAfterPaymentRequiredButStillTriesGroq()
    {
        var groqAttempted = false;
        var secondaryGeminiAttempted = false;
        var attempts = new[]
        {
            Attempt("Gemini", "gemini-4-hydrogen", _ =>
                throw new HttpRequestException("payment required", null, HttpStatusCode.PaymentRequired)),
            Attempt("Groq", "qwen/qwen3.8-27b", _ =>
            {
                groqAttempted = true;
                return Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray(new JsonObject { ["description"] = "Adapter" }) });
            }),
            Attempt("Gemini", "gemini-3.8-flash", _ =>
            {
                secondaryGeminiAttempted = true;
                return Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray(new JsonObject { ["description"] = "Should not run" }) });
            })
        };

        var result = await VisionProviderFallback.ExecuteAsync(
            "SupplierInvoice", attempts, HasLines, NullLogger.Instance, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(groqAttempted);
        Assert.False(secondaryGeminiAttempted);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesToGeminiWhenGroqFails()
    {
        var order = new List<string>();
        var attempts = new[]
        {
            Attempt("Gemini", "gemini-4-hydrogen", _ =>
            {
                order.Add("Gemini-primary");
                throw new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable);
            }),
            Attempt("Groq", "qwen/qwen3.8-27b", _ =>
            {
                order.Add("Groq");
                throw new HttpRequestException("bad request", null, HttpStatusCode.BadRequest);
            }),
            Attempt("Gemini", "gemini-3.8-flash", _ =>
            {
                order.Add("Gemini-secondary");
                return Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray(new JsonObject { ["description"] = "Adapter" }) });
            })
        };

        var result = await VisionProviderFallback.ExecuteAsync(
            "SupplierInvoice", attempts, HasLines, NullLogger.Instance, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(new[] { "Gemini-primary", "Groq", "Gemini-secondary" }, order);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesToNextGeminiModelAfterRateLimit()
    {
        var secondaryGeminiAttempted = false;
        var attempts = new[]
        {
            Attempt("Gemini", "gemini-4-hydrogen", _ =>
                throw new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests)),
            Attempt("Gemini", "gemini-3.8-flash", _ =>
            {
                secondaryGeminiAttempted = true;
                return Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray(new JsonObject { ["description"] = "Adapter" }) });
            })
        };

        var result = await VisionProviderFallback.ExecuteAsync(
            "SupplierInvoice", attempts, HasLines, NullLogger.Instance, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(secondaryGeminiAttempted);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsRemainingGeminiModelsAfterCredentialFailure()
    {
        var groqAttempted = false;
        var secondaryGeminiAttempted = false;
        var attempts = new[]
        {
            Attempt("Gemini", "gemini-4-hydrogen", _ =>
                throw new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized)),
            Attempt("Groq", "qwen/qwen3.8-27b", _ =>
            {
                groqAttempted = true;
                return Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray(new JsonObject { ["description"] = "Adapter" }) });
            }),
            Attempt("Gemini", "gemini-3.8-flash", _ =>
            {
                secondaryGeminiAttempted = true;
                return Task.FromResult<JsonNode?>(new JsonObject());
            })
        };

        var result = await VisionProviderFallback.ExecuteAsync(
            "SupplierInvoice", attempts, HasLines, NullLogger.Instance, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(groqAttempted);
        Assert.False(secondaryGeminiAttempted);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotAcceptEmptyOrUnusableStructuredOutput()
    {
        var fallbackCalled = false;
        var attempts = new[]
        {
            Attempt("Gemini", "gemini-4-hydrogen", _ =>
                Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray() })),
            Attempt("Groq", "qwen/qwen3.8-27b", _ =>
            {
                fallbackCalled = true;
                return Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray(new JsonObject { ["description"] = "Charger" }) });
            })
        };

        var result = await VisionProviderFallback.ExecuteAsync(
            "SupplierInvoice", attempts, HasLines, NullLogger.Instance, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(fallbackCalled);
    }

    [Fact]
    public async Task ExecuteAsync_StopsWhenTotalProviderBudgetIsExhausted()
    {
        var fallbackCalled = false;
        var attempts = new[]
        {
            Attempt("Gemini", "gemini-4-hydrogen", async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                return new JsonObject { ["lines"] = new JsonArray() };
            }),
            Attempt("Groq", "qwen/qwen3.8-27b", _ =>
            {
                fallbackCalled = true;
                return Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray() });
            })
        };

        var result = await VisionProviderFallback.ExecuteAsync(
            "SupplierInvoice", attempts, HasLines, NullLogger.Instance, CancellationToken.None,
            TimeSpan.FromSeconds(1));

        Assert.Null(result);
        Assert.False(fallbackCalled);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotFallBackAfterSafetyRefusal()
    {
        var groqCalled = false;
        var attempts = new[]
        {
            Attempt("Gemini", "gemini-4-hydrogen", _ =>
                throw new VisionProviderRefusalException("provider safety refusal")),
            Attempt("Groq", "qwen/qwen3.8-27b", _ =>
            {
                groqCalled = true;
                return Task.FromResult<JsonNode?>(new JsonObject { ["lines"] = new JsonArray() });
            })
        };

        var result = await VisionProviderFallback.ExecuteAsync(
            "SupplierInvoice", attempts, HasLines, NullLogger.Instance, CancellationToken.None);

        Assert.Null(result);
        Assert.False(groqCalled);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCallerCancellationWithoutTryingFallback()
    {
        var fallbackCalled = false;
        using var cts = new CancellationTokenSource();
        var attempts = new[]
        {
            Attempt("Gemini", "gemini-4-hydrogen", _ => throw new OperationCanceledException()),
            Attempt("Groq", "qwen/qwen3.8-27b", _ =>
            {
                fallbackCalled = true;
                return Task.FromResult<JsonNode?>(new JsonObject());
            })
        };
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            VisionProviderFallback.ExecuteAsync("SupplierInvoice", attempts, _ => true, NullLogger.Instance, cts.Token));

        Assert.False(fallbackCalled);
    }

    static VisionProviderAttempt Attempt(
        string provider, string model, Func<CancellationToken, Task<JsonNode?>> execute) =>
        new(provider, model, 5, execute);

    static bool HasLines(JsonNode? value) =>
        value?["lines"] is JsonArray { Count: > 0 } lines &&
        lines.Any(line => !string.IsNullOrWhiteSpace(line?["description"]?.GetValue<string>()));

    static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(x => x.Key, x => (string?)x.Value))
            .Build();
}
