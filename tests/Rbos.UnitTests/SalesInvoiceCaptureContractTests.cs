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
    public void GeminiResponseSchema_RequiresStructuredSerialArrayAndCoreInvoiceFields()
    {
        var schema = SalesInvoiceCaptureService.BuildResponseJsonSchema();
        var rootProperties = Assert.IsType<JsonObject>(schema["properties"]);
        var lineArray = Assert.IsType<JsonObject>(rootProperties["lines"]);
        var lineSchema = Assert.IsType<JsonObject>(lineArray["items"]);
        var lineProperties = Assert.IsType<JsonObject>(lineSchema["properties"]);
        var serialSchema = Assert.IsType<JsonObject>(lineProperties["serialNumbers"]);
        var required = Assert.IsType<JsonArray>(lineSchema["required"]);

        Assert.Equal("array", serialSchema["type"]!.GetValue<string>());
        var serialItemSchema = Assert.IsType<JsonObject>(serialSchema["items"]);
        Assert.Equal("string", serialItemSchema["type"]!.GetValue<string>());
        Assert.Contains(required, item => item?.GetValue<string>() == "serialNumbers");
        Assert.Contains(required, item => item?.GetValue<string>() == "description");
    }

}
