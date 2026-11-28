using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rbos.Api.Application;
using SkiaSharp;

namespace Rbos.UnitTests;

/// <summary>
/// One manual, synthetic-image integration check for the direct Anthropic Messages API.
/// It is intentionally excluded from normal PR CI and is run only by the manual workflow.
/// </summary>
public sealed class AnthropicHaikuLiveIntegrationTests
{
    [Fact]
    [Trait("Category", "AnthropicLiveIntegration")]
    public async Task Reads_one_synthetic_invoice_image_with_the_live_messages_api_once()
    {
        // Never make paid API calls during normal unit/PR CI, even if the secret exists.
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RBOS_ENABLE_ANTHROPIC_LIVE_SMOKE"),
                "true", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Skipped: this paid integration check runs only from its explicit manual workflow.");
            return;
        }

        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Assert.False(string.IsNullOrWhiteSpace(apiKey),
            "Set the GitHub Actions secret ANTHROPIC_API_KEY and trigger the manual smoke workflow.");

        var sourceImage = CreateSyntheticInvoiceImage();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = apiKey,
                ["Anthropic:SalesInvoiceModel"] = "claude-haiku-5-5",
                ["Anthropic:ApiVersion"] = "2023-06-01",
                ["Anthropic:SalesInvoiceMaxOutputTokens"] = "256"
            })
            .Build();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var client = new AnthropicHaikuClient(
            http, configuration, NullLogger<AnthropicHaikuClient>.Instance);
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["invoiceNumber"] = new JsonObject { ["type"] = "string" },
                ["total"] = new JsonObject { ["type"] = "string" }
            },
            ["required"] = new JsonArray("invoiceNumber", "total"),
            ["additionalProperties"] = false
        };

        AnthropicSalesInvoiceResult? result;
        try
        {
            // Exactly one network call. No retry, no customer data, no production bill.
            result = await client.ExtractAsync(
                "image/png", sourceImage, schema,
                "This is a synthetic RBOS integration-test invoice, not a customer document. " +
                "Read the printed Invoice No. and TOTAL from the image. Return invoiceNumber " +
                "and total as strings exactly as printed. Do not invent other fields.",
                CancellationToken.None);
        }
        catch (AnthropicApiException error)
        {
            throw new InvalidOperationException(
                $"Anthropic live integration failed. HTTP {(int?)error.StatusCode}; " +
                $"request {error.RequestId ?? "unavailable"}; {error.Message}", error);
        }

        Assert.NotNull(result);
        Assert.Equal("claude-haiku-5-5", result.Model);
        Assert.Equal("SMOKE-001", result.Extraction["invoiceNumber"]?.GetValue<string>());
        Assert.Equal("123.45", result.Extraction["total"]?.GetValue<string>());
        Assert.True(result.InputTokens > 0);
        Assert.True(result.OutputTokens > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.RequestId));
        Console.WriteLine(
            $"Anthropic live check passed: model={result.Model}, inputTokens={result.InputTokens}, " +
            $"outputTokens={result.OutputTokens}, requestId={result.RequestId}");
    }

    static byte[] CreateSyntheticInvoiceImage()
    {
        using var bitmap = new SKBitmap(760, 250);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };
        using var regularFont = new SKFont(SKTypeface.Default, 38);
        using var invoiceFont = new SKFont(SKTypeface.Default, 34);
        using var totalTypeface = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold);
        using var totalFont = new SKFont(totalTypeface, 40);
        canvas.DrawText("RBOS SYNTHETIC TEST BILL", 24, 62, SKTextAlign.Left, regularFont, paint);
        canvas.DrawText("Invoice No: SMOKE-001", 24, 128, SKTextAlign.Left, invoiceFont, paint);
        canvas.DrawText("TOTAL: 123.45", 24, 198, SKTextAlign.Left, totalFont, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}