using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public sealed class SalesInvoiceCaptureContractTests
{
    [Fact]
    public void SalesInvoiceShadowReaders_ReuseExistingKeysOnlyWhenExplicitlyEnabled()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "existing-gemini-key",
            ["Groq:ApiKey"] = "existing-groq-key",
            ["Gemini:SalesInvoiceFreeTierApiKey"] = "obsolete-dedicated-gemini-key",
            ["Groq:SalesInvoiceFreeTierApiKey"] = "obsolete-dedicated-groq-key",
            ["Gemini:SalesInvoiceHydrogenModel"] = "paid-or-unapproved-model",
            ["Groq:Model"] = "paid-or-unapproved-model"
        }).Build();

        Assert.Null(SalesInvoiceCaptureService.GetSalesInvoiceGeminiShadowApiKey(configuration));
        Assert.Null(SalesInvoiceCaptureService.GetSalesInvoiceGroqShadowApiKey(configuration));

        configuration["Gemini:SalesInvoiceShadowEnabled"] = "true";
        configuration["Groq:SalesInvoiceShadowEnabled"] = "true";

        Assert.Equal("existing-gemini-key", SalesInvoiceCaptureService.GetSalesInvoiceGeminiShadowApiKey(configuration));
        Assert.Equal("existing-groq-key", SalesInvoiceCaptureService.GetSalesInvoiceGroqShadowApiKey(configuration));
        Assert.Equal("gemini-4-hydrogen", SalesInvoiceCaptureService.GeminiFreeTierModel);
        Assert.Equal("qwen/qwen3.8-27b", SalesInvoiceCaptureService.GroqFreeTierVisionModel);
    }

    [Fact]
    public void SalesInvoiceShadowReaders_DoNotUseLegacyDedicatedKeysOrPaidModelOverrides()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:SalesInvoiceShadowEnabled"] = "true",
            ["Groq:SalesInvoiceShadowEnabled"] = "true",
            ["Gemini:SalesInvoiceFreeTierApiKey"] = "legacy-gemini-key",
            ["Groq:SalesInvoiceFreeTierApiKey"] = "legacy-groq-key",
            ["Gemini:SalesInvoiceHydrogenModel"] = "gemini-4-argon",
            ["Groq:Model"] = "paid-model"
        }).Build();

        Assert.Null(SalesInvoiceCaptureService.GetSalesInvoiceGeminiShadowApiKey(configuration));
        Assert.Null(SalesInvoiceCaptureService.GetSalesInvoiceGroqShadowApiKey(configuration));
        Assert.Equal("gemini-4-hydrogen", SalesInvoiceCaptureService.GeminiFreeTierModel);
        Assert.Equal("qwen/qwen3.8-27b", SalesInvoiceCaptureService.GroqFreeTierVisionModel);
    }

    [Theory]
    [InlineData(true, true, true, "Anthropic")]
    [InlineData(false, true, true, "Gemini")]
    [InlineData(false, false, true, "Groq")]
    [InlineData(false, false, false, "None")]
    public void SalesInvoiceProviderSelection_PrefersHaikuThenGeminiThenGroq(
        bool haikuUsable, bool geminiUsable, bool groqUsable, string expected)
    {
        Assert.Equal(expected,
            SalesInvoiceCaptureService.SelectSalesInvoiceProvider(haikuUsable, geminiUsable, groqUsable));
    }

    [Fact]
    public void ProviderComparison_DoesNotCompareWhenEitherProviderHasNoUsableExtraction()
    {
        var usable = JsonNode.Parse("""
            {
              "lines": [{ "description": "Washing machine", "quantity": 1 }]
            }
            """)!;

        var comparison = SalesInvoiceCaptureService.CompareProviderExtractions(usable, null);

        Assert.Equal("not_compared", comparison["status"]!.GetValue<string>());
        Assert.Equal(0, comparison["mismatchFieldCount"]!.GetValue<int>());
        Assert.Empty(Assert.IsType<JsonArray>(comparison["mismatchedFields"]));
    }

    [Fact]
    public async Task ExtractAsync_StartsGeminiAndGroqShadowReadersConcurrently()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-gemini-key",
            ["Gemini:SalesInvoiceShadowEnabled"] = "true",
            ["Groq:ApiKey"] = "test-groq-key",
            ["Groq:SalesInvoiceShadowEnabled"] = "true",
            ["AiPerformance:SalesInvoiceTotalProviderBudgetSeconds"] = "10",
            ["AiPerformance:SalesInvoiceShadowAttemptTimeoutSeconds"] = "5"
        }).Build();
        var handler = new ParallelVisionHandler();
        using var http = new HttpClient(handler);
        var haiku = new AnthropicHaikuClient(
            new HttpClient(new StubHttpMessageHandler()),
            configuration,
            NullLogger<AnthropicHaikuClient>.Instance);
        var groq = new GroqVisionClient(http, configuration, NullLogger<GroqVisionClient>.Instance);
        var service = new SalesInvoiceCaptureService(
            http, haiku, groq, configuration, NullLogger<SalesInvoiceCaptureService>.Instance);

        await service.ExtractAsync(
            "image/jpeg",
            [0xFF, 0xD8, 0xFF, 0xD9],
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(handler.BothProvidersStarted, "Gemini and Groq must both start before either response is released.");
        Assert.True(handler.GeminiUsesLowThinking);
        Assert.True(handler.GeminiOutputIsBounded);
        Assert.True(handler.GroqUsesInstructMode);
        Assert.True(handler.GroqOutputIsBounded);
        Assert.Equal(handler.GeminiPrompt, handler.GroqPrompt);
        Assert.Contains("Return JSON only", handler.GeminiPrompt ?? string.Empty);
    }

    [Fact]
    public void ProviderCandidateDifferences_ShowMismatchedBusinessFieldsButExcludeCustomerFields()
    {
        var haiku = JsonNode.Parse("""
            {
              "customerName": "Private Customer",
              "customerPhone": "9999999999",
              "invoiceNumber": "00454",
              "total": 17500,
              "lines": [{
                "description": "Voltas Beko WM",
                "quantity": 1,
                "amount": 17500
              }]
            }
            """)!;
        var gemini = JsonNode.Parse("""
            {
              "customerName": "Another Private Customer",
              "customerPhone": "8888888888",
              "invoiceNumber": "00454",
              "total": 17000,
              "lines": [{
                "description": "Voltas Beko WPM",
                "quantity": 1,
                "amount": 17500
              }]
            }
            """)!;

        var differences = SalesInvoiceCaptureService.BuildProviderCandidateDifferences(haiku, gemini, null);
        var json = differences.ToJsonString();

        Assert.Contains("total", json, StringComparison.Ordinal);
        Assert.Contains("lines[0].description", json, StringComparison.Ordinal);
        Assert.DoesNotContain("customerName", json, StringComparison.Ordinal);
        Assert.DoesNotContain("customerPhone", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Private Customer", json, StringComparison.Ordinal);
        Assert.DoesNotContain("9999999999", json, StringComparison.Ordinal);
    }

    [Fact]
    public void HaikuPeerAssessment_DetectsWhenBothShadowReadersAgreeAgainstHaiku()
    {
        static JsonObject Comparison(params string[] fields) => new()
        {
            ["mismatchFieldCount"] = fields.Length,
            ["mismatchedFields"] = new JsonArray(fields.Select(field => (JsonNode?)JsonValue.Create(field)).ToArray())
        };

        var haikuGemini = Comparison("total", "lines[0].description");
        var haikuGroq = Comparison("total", "lines[0].quantity");
        var geminiGroq = Comparison("lines[0].description", "lines[0].quantity");

        var consensus = SalesInvoiceCaptureService.FindShadowConsensusDifferences(
            haikuGemini, haikuGroq, geminiGroq);
        var assessment = SalesInvoiceCaptureService.GetHaikuPeerAssessment(
            true, true, true, haikuGemini, haikuGroq, geminiGroq, consensus);

        Assert.Equal(new[] { "total" }, consensus);
        Assert.Equal("haiku_differs_from_both_shadow_readers", assessment);
    }

    [Fact]
    public void ProviderComparison_ReportsOnlyFieldNamesAndNeverExtractedValues()
    {
        var haiku = JsonNode.Parse("""
            {
              "invoiceNumber": "00454",
              "date": "2026-11-06",
              "total": 17500,
              "lines": [{
                "description": "Voltas Beko WM",
                "quantity": 1,
                "unitPrice": null,
                "amount": 17500,
                "fieldStates": { "unitPrice": "not_present" },
                "identifierCandidates": []
              }]
            }
            """)!;
        var groq = JsonNode.Parse("""
            {
              "invoiceNumber": "00454",
              "date": "2026-11-06",
              "total": 17000,
              "lines": [{
                "description": "Voltas Beko WPM",
                "quantity": 1,
                "unitPrice": null,
                "amount": 17500,
                "fieldStates": { "unitPrice": "not_present" },
                "identifierCandidates": []
              }]
            }
            """)!;

        var comparison = SalesInvoiceCaptureService.CompareProviderExtractions(haiku, groq);

        Assert.Equal("compared", comparison["status"]!.GetValue<string>());
        Assert.Equal(2, comparison["mismatchFieldCount"]!.GetValue<int>());
        var fields = Assert.IsType<JsonArray>(comparison["mismatchedFields"])
            .Select(item => item!.GetValue<string>())
            .ToArray();
        Assert.Contains("total", fields);
        Assert.Contains("lines[0].description", fields);
        Assert.DoesNotContain("17500", comparison.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("17000", comparison.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SalesInvoiceExtraction_UsesOneVersionedPromptForAllVisionReaders()
    {
        Assert.Equal("sales-invoice-vision-v2", SalesInvoiceCaptureService.ExtractionPromptVersion);
    }

    [Fact]
    public void NormalizeForReview_PreservesVerbatimDescriptionAndKeepsLegacySerialUnconfirmed()
    {
        var source = JsonNode.Parse("""
            {
              "invoiceNumber": "00454",
              "date": "2026-11-06",
              "lines": [{
                "description": "Voltas Beko WM\nModel WTL8066\nS/N SN12345678",
                "brand": "Voltas Beko",
                "model": "WTL8066",
                "serialNumbers": ["SN12345678"],
                "quantity": 1,
                "unitPrice": 17500,
                "amount": 17500,
                "confidence": 0.8
              }]
            }
            """)!;

        var result = SalesInvoiceCaptureService.NormalizeForReview(source);
        var line = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(result["lines"])));

        Assert.Equal("00454", result["invoiceNumber"]!.GetValue<string>());
        Assert.Equal("Voltas Beko WM\nModel WTL8066\nS/N SN12345678", line["description"]!.GetValue<string>());
        Assert.Equal("WTL8066", line["model"]!.GetValue<string>());
        Assert.Empty(Assert.IsType<JsonArray>(line["serialNumbers"]));
        var candidate = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(line["identifierCandidates"])));
        Assert.Equal("SN12345678", candidate["value"]!.GetValue<string>());
        Assert.Equal("unknown", candidate["classification"]!.GetValue<string>());
        Assert.Equal("ambiguous", candidate["fieldState"]!.GetValue<string>());
        Assert.False(candidate["confirmed"]!.GetValue<bool>());
        Assert.Equal(17500m, line["amount"]!.GetValue<decimal>());
    }

    [Fact]
    public void NormalizeForReview_ConvertsLegacySerialArraysIntoUniqueUnconfirmedCandidates()
    {
        var source = JsonNode.Parse("""
            {
              "lines": [{
                "description": "LG refrigerator",
                "serialNumbers": ["SN12345678", "SN12345678", "SN98765432"],
                "serialNumber": "SN12345678",
                "quantity": 1
              }]
            }
            """)!;

        var result = SalesInvoiceCaptureService.NormalizeForReview(source);
        var line = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(result["lines"])));
        var serials = Assert.IsType<JsonArray>(line["serialNumbers"]);
        var candidates = Assert.IsType<JsonArray>(line["identifierCandidates"]);

        Assert.Empty(serials);
        Assert.Equal(2, candidates.Count);
        Assert.Equal("SN12345678", candidates[0]!["value"]!.GetValue<string>());
        Assert.Equal("SN98765432", candidates[1]!["value"]!.GetValue<string>());
        Assert.All(candidates, candidate =>
        {
            Assert.Equal("unknown", candidate!["classification"]!.GetValue<string>());
            Assert.Equal("ambiguous", candidate["fieldState"]!.GetValue<string>());
            Assert.False(candidate["confirmed"]!.GetValue<bool>());
        });
    }

    [Fact]
    public void NormalizeForReview_ReturnsAnEmptySerialListWhenTheModelCannotIdentifyOne()
    {
        var source = JsonNode.Parse("""{"lines":[{"description":"Washing machine","quantity":1}]}""")!;

        var result = SalesInvoiceCaptureService.NormalizeForReview(source);
        var line = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(result["lines"])));

        Assert.Empty(Assert.IsType<JsonArray>(line["serialNumbers"]));
    }
    [Fact]
    public void NormalizeForReview_DoesNotClaimSourceReadWhenProviderOmitsProvenance()
    {
        var source = JsonNode.Parse("""
            {
              "invoiceNumber": "00454",
              "lines": [{ "description": "Voltas washer", "quantity": 1, "serialNumbers": ["SN123456"] }]
            }
            """)!;

        var result = SalesInvoiceCaptureService.NormalizeForReview(source);
        var line = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(result["lines"])));
        var candidate = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(line["identifierCandidates"])));

        Assert.Equal("ambiguous", result["fieldStates"]!["invoiceNumber"]!.GetValue<string>());
        Assert.Equal("ambiguous", line["fieldStates"]!["description"]!.GetValue<string>());
        Assert.Equal("ambiguous", candidate["fieldState"]!.GetValue<string>());
        Assert.Equal("unreadable", result["fieldStates"]!["total"]!.GetValue<string>());
    }

    [Fact]
    public void NormalizeForReview_PreservesOnlyBoundedFieldConfidenceScores()
    {
        var source = JsonNode.Parse("""
            {
              "fieldConfidence": { "invoiceNumber": 0.91, "date": 1.2, "customerName": -0.1 },
              "lines": [{
                "description": "Cable",
                "fieldConfidence": { "description": 0.42, "quantity": 0.95, "unitPrice": 1.1 }
              }]
            }
            """)!;

        var result = SalesInvoiceCaptureService.NormalizeForReview(source);
        var header = Assert.IsType<JsonObject>(result["fieldConfidence"]);
        var line = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(result["lines"])));
        var lineConfidence = Assert.IsType<JsonObject>(line["fieldConfidence"]);

        Assert.Equal(0.91m, header["invoiceNumber"]!.GetValue<decimal>());
        Assert.Null(header["date"]);
        Assert.Null(header["customerName"]);
        Assert.Equal(0.42m, lineConfidence["description"]!.GetValue<decimal>());
        Assert.Equal(0.95m, lineConfidence["quantity"]!.GetValue<decimal>());
        Assert.Null(lineConfidence["unitPrice"]);
    }

    [Fact]
    public void BuildReviewIssues_FlagsLowConfidenceAndLineAmountMismatch()
    {
        var source = JsonNode.Parse("""
            {
              "invoiceNumber": "00454", "date": "2026-11-06",
              "customerName": "Test Customer", "customerPhone": "9876543210",
              "total": 250,
              "fieldConfidence": { "invoiceNumber": 0.95, "date": 0.95, "customerName": 0.95,
                "customerPhone": 0.95, "total": 0.95 },
              "lines": [{
                "description": "Copper cable", "quantity": 2, "unitPrice": 100,
                "amount": 150, "serialNumbers": [],
                "fieldConfidence": { "description": 0.45, "quantity": 0.95, "unitPrice": 0.95,
                  "amount": 0.95, "serialNumbers": 1 }
              }]
            }
            """)!;
        var extraction = SalesInvoiceCaptureService.NormalizeForReview(source);

        var issues = SalesInvoiceCaptureService.BuildReviewIssues(extraction);

        Assert.Contains(issues, issue => issue.Field == "description" && issue.LineIndex == 0);
        Assert.Contains(issues, issue => issue.Field == "unitPrice" && issue.LineIndex == 0 &&
            issue.Message.Contains("quantity × unit price", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue => issue.Field == "serialNumbers");
    }

    [Fact]
    public void BuildReviewIssues_FlagsDuplicateSerialsAcrossDifferentRows()
    {
        var source = JsonNode.Parse("""
            {
              "invoiceNumber": "00454", "date": "2026-11-06",
              "customerName": "Test Customer", "customerPhone": "9876543210",
              "total": 300,
              "lines": [
                { "description": "Television", "quantity": 1, "unitPrice": 200, "identifierCandidates": [{ "value": "SN123456", "classification": "serial" }] },
                { "description": "Washer", "quantity": 1, "unitPrice": 100, "identifierCandidates": [{ "value": "SN123456", "classification": "serial" }] }
              ]
            }
            """)!;
        var extraction = SalesInvoiceCaptureService.NormalizeForReview(source);

        var issues = SalesInvoiceCaptureService.BuildReviewIssues(extraction);

        Assert.Equal(2, issues.Count(issue => issue.Field == "identifierCandidates" &&
            issue.Message.Contains("appears on more than one item", StringComparison.Ordinal)));
        Assert.DoesNotContain(issues, issue => issue.Field == "serialNumbers");
    }

    [Fact]
    public void BuildReviewIssues_FlagsLineAmountsThatDoNotMatchSubtotal()
    {
        var source = JsonNode.Parse("""
            {
              "invoiceNumber": "00454", "date": "2026-11-06",
              "customerName": "Test Customer", "customerPhone": "9876543210",
              "subtotal": 250, "taxAmount": 0, "total": 250,
              "lines": [
                { "description": "Copper cable", "quantity": 1, "unitPrice": 240, "amount": 240 }
              ]
            }
            """)!;
        var extraction = SalesInvoiceCaptureService.NormalizeForReview(source);

        var issues = SalesInvoiceCaptureService.BuildReviewIssues(extraction);

        Assert.Contains(issues, issue => issue.Field == "subtotal" &&
            issue.Message.Contains("sum of written line amounts", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildReviewIssues_FlagsInvalidSerialsAndDiscountsBeforeAdding()
    {
        var source = JsonNode.Parse("""
            {
              "invoiceNumber": "00454", "date": "2026-11-06",
              "customerName": "Test Customer", "customerPhone": "9876543210",
              "total": 200,
              "lines": [{
                "description": "Television", "quantity": 1, "unitPrice": 200,
                "discount": 250, "identifierCandidates": [{ "value": "BAD SN", "classification": "serial" }]
              }]
            }
            """)!;
        var extraction = SalesInvoiceCaptureService.NormalizeForReview(source);

        var issues = SalesInvoiceCaptureService.BuildReviewIssues(extraction);

        Assert.Contains(issues, issue => issue.Field == "discount" && issue.LineIndex == 0);
        Assert.DoesNotContain(issues, issue => issue.Field == "serialNumbers");
        Assert.DoesNotContain(issues, issue => issue.Message.Contains("IMEI", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildReviewIssues_DoesNotApplyImeiLuhnToSerialNumbers()
    {
        const string digits = "123456789012345";
        Assert.False(SalesInvoiceCaptureService.IsValidImei(digits));
        var source = JsonNode.Parse("""
            {
              "lines": [{
                "description": "Device",
                "quantity": 1,
                "unitPrice": 100,
                "identifierCandidates": [{ "value": "123456789012345", "classification": "serial" }]
              }]
            }
            """)!;
        var extraction = SalesInvoiceCaptureService.NormalizeForReview(source);

        var issues = SalesInvoiceCaptureService.BuildReviewIssues(extraction);

        Assert.DoesNotContain(issues, issue => issue.Message.Contains("IMEI", StringComparison.Ordinal));
        Assert.Empty(Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(extraction["lines"])))["serialNumbers"]));
    }

    [Fact]
    public void BuildReviewIssues_ValidatesLuhnOnlyForPossibleImeis()
    {
        var source = JsonNode.Parse("""
            {
              "lines": [{
                "description": "Phone",
                "quantity": 1,
                "unitPrice": 100,
                "identifierCandidates": [{ "value": "123456789012345", "classification": "imei" }]
              }]
            }
            """)!;
        var extraction = SalesInvoiceCaptureService.NormalizeForReview(source);

        var issues = SalesInvoiceCaptureService.BuildReviewIssues(extraction);

        Assert.Contains(issues, issue => issue.Field == "identifierCandidates" &&
            issue.Message.Contains("possible IMEI failed its check digit", StringComparison.Ordinal));
    }

    [Fact]
    public void NormalizeForReview_PreservesCompleteLongVerbatimProductDescription()
    {
        var description = string.Join("\n", Enumerable.Repeat(
            "Samsung LED Television model UA43K5570 serial characters O0I1 and package specification details",
            8));
        var source = new JsonObject
        {
            ["lines"] = new JsonArray(new JsonObject
            {
                ["description"] = description,
                ["quantity"] = 1,
                ["unitPrice"] = 100m,
                ["fieldStates"] = new JsonObject
                {
                    ["description"] = "source_read",
                    ["quantity"] = "source_read",
                    ["unitPrice"] = "source_read"
                }
            })
        };

        var result = SalesInvoiceCaptureService.NormalizeForReview(source);
        var line = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(result["lines"])));
        Assert.Equal(description, line["description"]!.GetValue<string>());
        Assert.Equal(description.Length, line["description"]!.GetValue<string>().Length);
        Assert.Equal(100m, line["sellingPrice"]!.GetValue<decimal>());
        Assert.Equal("source_read", line["sellingPriceProvenance"]!.GetValue<string>());
    }

    [Fact]
    public void NormalizeForReview_PreservesBlankRateAsNotPresentRatherThanUnreadable()
    {
        var source = JsonNode.Parse("""
            {
              "lines": [{
                "description": "Voltas washing machine",
                "quantity": 1,
                "unitPrice": "",
                "amount": "17500",
                "fieldStates": { "description": "source_read", "quantity": "source_read", "unitPrice": "not_present", "amount": "source_read" }
              }]
            }
            """)!;
        var extraction = SalesInvoiceCaptureService.NormalizeForReview(source);
        var line = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(extraction["lines"])));

        Assert.Null(line["unitPrice"]);
        Assert.Equal("not_present", line["fieldStates"]!["unitPrice"]!.GetValue<string>());
        Assert.Null(line["sellingPrice"]);
        Assert.Equal("not_present", line["sellingPriceProvenance"]!.GetValue<string>());
        Assert.Equal(17500m, line["amount"]!.GetValue<decimal>());
        var issues = SalesInvoiceCaptureService.BuildReviewIssues(extraction);
        Assert.Contains(issues, issue => issue.Field == "unitPrice" && issue.Severity == "info" &&
            issue.Message.Contains("source Rate cell is blank", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue => issue.Field == "unitPrice" &&
            issue.Message.Contains("needs a selling rate greater than zero", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"00454\"", "00454")]
    [InlineData("10020", "10020")]
    [InlineData("10020.50", "10020.50")]
    public void ReadFieldValueText_PreservesStringHeadersAndSerializesNumericTotals(string json, string expected)
    {
        var value = JsonNode.Parse(json);
        Assert.Equal(expected, SalesInvoiceCaptureService.ReadFieldValueText(value));
    }

    [Fact]
    public void NormalizeForReview_DoesNotGuessNotPresentWhenNullRateHasNoExplicitSourceState()
    {
        var source = JsonNode.Parse("""
            {
              "lines": [{
                "description": "Voltas washing machine",
                "quantity": 1,
                "unitPrice": null,
                "amount": 17500
              }]
            }
            """)!;
        var extraction = SalesInvoiceCaptureService.NormalizeForReview(source);
        var line = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(extraction["lines"])));

        Assert.Null(line["unitPrice"]);
        Assert.Equal("unreadable", line["fieldStates"]!["unitPrice"]!.GetValue<string>());
        var issues = SalesInvoiceCaptureService.BuildReviewIssues(extraction);
        Assert.Contains(issues, issue => issue.Field == "unitPrice" &&
            issue.Message.Contains("needs a selling rate greater than zero", StringComparison.Ordinal));
        Assert.DoesNotContain(issues, issue => issue.Message.Contains("source Rate cell is blank", StringComparison.Ordinal));
    }

    [Fact]
    public void AnthropicSchemaAvoidsUnsupportedConstraintsAndExcessiveUnionTypes()
    {
        var schema = SalesInvoiceCaptureService.BuildAnthropicResponseJsonSchema();
        var json = schema.ToJsonString();

        Assert.DoesNotContain("\"minimum\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"maximum\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"maxItems\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"type\":[", json, StringComparison.Ordinal);
        Assert.Contains("fieldStates", json, StringComparison.Ordinal);
        Assert.Contains("identifierCandidates", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeminiResponseSchema_RequiresStructuredSerialArrayAndCoreInvoiceFields()
    {
        var schema = SalesInvoiceCaptureService.BuildResponseJsonSchema();
        var rootProperties = Assert.IsType<JsonObject>(schema["properties"]);
        var lineArray = Assert.IsType<JsonObject>(rootProperties["lines"]);
        var lineSchema = Assert.IsType<JsonObject>(lineArray["items"]);
        var lineProperties = Assert.IsType<JsonObject>(lineSchema["properties"]);
        var serialSchema = Assert.IsType<JsonObject>(lineProperties["serialNumbers"]);
        var lineConfidence = Assert.IsType<JsonObject>(lineProperties["fieldConfidence"]);
        var headerConfidence = Assert.IsType<JsonObject>(rootProperties["fieldConfidence"]);
        var required = Assert.IsType<JsonArray>(lineSchema["required"]);

        Assert.Contains(required, item => item?.GetValue<string>() == "fieldConfidence");
        Assert.Contains(Assert.IsType<JsonArray>(schema["required"]), item => item?.GetValue<string>() == "fieldConfidence");
        Assert.Contains(Assert.IsType<JsonObject>(lineConfidence["properties"]), pair => pair.Key == "description");
        Assert.Contains(Assert.IsType<JsonObject>(headerConfidence["properties"]), pair => pair.Key == "total");
        Assert.Equal("array", serialSchema["type"]!.GetValue<string>());
        var serialItemSchema = Assert.IsType<JsonObject>(serialSchema["items"]);
        Assert.Equal("string", serialItemSchema["type"]!.GetValue<string>());
        Assert.Contains(required, item => item?.GetValue<string>() == "serialNumbers");
        Assert.Contains(required, item => item?.GetValue<string>() == "description");
    }


    private sealed class ParallelVisionHandler : HttpMessageHandler
    {
        readonly TaskCompletionSource<bool> bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int started;

        public bool BothProvidersStarted => Volatile.Read(ref started) >= 2;
        public bool GeminiUsesLowThinking { get; private set; }
        public bool GeminiOutputIsBounded { get; private set; }
        public bool GroqUsesInstructMode { get; private set; }
        public bool GroqOutputIsBounded { get; private set; }
        public string? GeminiPrompt { get; private set; }
        public string? GroqPrompt { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref started) == 2)
                bothStarted.TrySetResult(true);

            await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            var requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var requestJson = JsonNode.Parse(requestBody)!;
            var isGemini = request.RequestUri?.Host == "generativelanguage.googleapis.com";
            if (isGemini)
            {
                GeminiUsesLowThinking = requestJson["generationConfig"]?["thinkingConfig"]?["thinkingLevel"]?.GetValue<string>() == "LOW";
                GeminiOutputIsBounded = requestJson["generationConfig"]?["maxOutputTokens"]?.GetValue<int>() == 8192;
                GeminiPrompt = requestJson["contents"]?[0]?["parts"]?[0]?["text"]?.GetValue<string>();
            }
            else
            {
                GroqUsesInstructMode = requestJson["reasoning_effort"]?.GetValue<string>() == "none";
                GroqOutputIsBounded = requestJson["max_completion_tokens"]?.GetValue<int>() == 8192;
                GroqPrompt = requestJson["messages"]?[0]?["content"]?[0]?["text"]?.GetValue<string>();
            }
            var extraction = "{\"lines\":[{\"description\":\"Test item\",\"quantity\":1,\"amount\":100}]}";
            var body = isGemini
                ? "{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[{\"text\":\"" +
                    extraction.Replace("\"", "\\\"", StringComparison.Ordinal) +
                    "\"}]}}],\"usageMetadata\":{\"promptTokenCount\":12,\"candidatesTokenCount\":14}}"
                : "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"" +
                    extraction.Replace("\"", "\\\"", StringComparison.Ordinal) +
                    "\"}}]}";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Haiku must not be called when no Anthropic key is configured.");
    }

}
