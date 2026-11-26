using System.Text.Json.Nodes;
using Rbos.Api.Application;

namespace Rbos.UnitTests;

public sealed class SalesInvoiceCaptureContractTests
{
    [Fact]
    public void NormalizeForReview_PreservesVerbatimDescriptionAndStructuredSerialNumbers()
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
        Assert.Equal("SN12345678", Assert.Single(Assert.IsType<JsonArray>(line["serialNumbers"])).GetValue<string>());
        Assert.Equal(17500m, line["amount"]!.GetValue<decimal>());
    }

    [Fact]
    public void NormalizeForReview_AcceptsLegacySingleSerialAndDeduplicatesRepeatedValues()
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
        var serials = Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(result["lines"])))["serialNumbers"]);

        Assert.Equal(2, serials.Count);
        Assert.Equal("SN12345678", serials[0]!.GetValue<string>());
        Assert.Equal("SN98765432", serials[1]!.GetValue<string>());
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
                { "description": "Television", "quantity": 1, "unitPrice": 200, "serialNumbers": ["SN123456"] },
                { "description": "Washer", "quantity": 1, "unitPrice": 100, "serialNumbers": ["SN123456"] }
              ]
            }
            """)!;
        var extraction = SalesInvoiceCaptureService.NormalizeForReview(source);

        var issues = SalesInvoiceCaptureService.BuildReviewIssues(extraction);

        Assert.Equal(2, issues.Count(issue => issue.Field == "serialNumbers" &&
            issue.Message.Contains("more than one item", StringComparison.Ordinal)));
        Assert.DoesNotContain(issues, issue => issue.Message.Contains("more serial numbers than its quantity", StringComparison.Ordinal));
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
                "discount": 250, "serialNumbers": ["BAD SN"]
              }]
            }
            """)!;
        var extraction = SalesInvoiceCaptureService.NormalizeForReview(source);

        var issues = SalesInvoiceCaptureService.BuildReviewIssues(extraction);

        Assert.Contains(issues, issue => issue.Field == "discount" && issue.LineIndex == 0);
        Assert.Contains(issues, issue => issue.Field == "serialNumbers" && issue.LineIndex == 0 &&
            issue.Message.Contains("accepted format", StringComparison.Ordinal));
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

}
