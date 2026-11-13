using System.Text;
using System.Text.Json;
using Rbos.Api.Application;
using QuestPDF.Infrastructure;

namespace Rbos.UnitTests;

public class InvoicePdfRendererTests
{
    static InvoicePdfRendererTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
    }

    static JsonDocument Snapshot(string gstRate, int count = 2)
    {
        var lines = string.Join(",", Enumerable.Range(1, count).Select(i =>
            $$"""{"name":"Product {{i}}","sku":"SKU-{{i}}","quantity":1,"unitPrice":100,"discount":0,"lineTotal":100,"serials":[]}"""));
        return JsonDocument.Parse($$"""
        {
          "sale":{"id":"00000000-0000-0000-0000-000000000001","invoiceNo":"INV-000001","date":"2026-11-13","saleType":"Cash","total":{{(count * 100)}},"amountPaid":{{(count * 100)}},"payMethod":"Cash","gstRate":{{gstRate}}},
          "business":{"businessName":"Test Electronics","address":"Main Road","city":"Kanpur","state":"Uttar Pradesh","pinCode":"208001","mobile":"9876543210","gstin":"09ABCDE1234F1Z5"},
          "customer":{"name":"ग्राहक Ravi","phone":"919876543210","notes":"Kanpur"},
          "location":"Main Store",
          "lines":[{{lines}}],
          "payments":[{"method":"Cash","amount":{{(count * 100)}}}]
        }
        """);
    }

    [Theory]
    [InlineData("18", 2)]
    [InlineData("null", 2)]
    [InlineData("5", 45)]
    public void Generates_valid_pdf_for_gst_non_gst_and_long_invoices(string gstRate, int lineCount)
    {
        using var snapshot = Snapshot(gstRate, lineCount);
        var pdf = InvoicePdfRenderer.Render(snapshot.RootElement);
        Assert.True(pdf.Length > 1000);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
    }
}
