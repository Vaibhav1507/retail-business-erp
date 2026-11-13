using System.Globalization;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Rbos.Api.Application;

/// <summary>Renders a deterministic PDF from the immutable sale-time invoice snapshot.</summary>
public static class InvoicePdfRenderer
{
    static string Text(JsonElement value, string name, string fallback = "")
        => value.TryGetProperty(name, out var p) && p.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? p.ToString() : fallback;

    static decimal Number(JsonElement value, string name)
        => value.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : 0m;

    static string Money(decimal value) => "₹" + value.ToString("N2", CultureInfo.GetCultureInfo("en-IN"));

    static readonly string[] Ones = ["", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"];
    static readonly string[] Tens = ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    static string Below100(int number) => number < 20
        ? Ones[number]
        : Tens[number / 10] + (number % 10 > 0 ? " " + Ones[number % 10] : "");

    static string Below1000(int number)
    {
        var hundreds = number / 100;
        var remainder = number % 100;
        return string.Join(" ", new[] {
            hundreds > 0 ? Ones[hundreds] + " Hundred" : "",
            remainder > 0 ? Below100(remainder) : ""
        }.Where(x => x.Length > 0));
    }

    static string AmountInWords(decimal amount)
    {
        var absolute = decimal.Abs(amount);
        var rupees = (long)decimal.Floor(absolute);
        var paise = (int)decimal.Round((absolute - rupees) * 100m, 0, MidpointRounding.AwayFromZero);
        if (rupees == 0 && paise == 0) return "Zero Rupees Only";
        var parts = new List<string>();
        var crore = rupees / 10_000_000;
        var lakh = (rupees / 100_000) % 100;
        var thousand = (rupees / 1_000) % 100;
        if (crore > 0) parts.Add(Below100((int)crore) + " Crore");
        if (lakh > 0) parts.Add(Below100((int)lakh) + " Lakh");
        if (thousand > 0) parts.Add(Below100((int)thousand) + " Thousand");
        if (rupees % 1_000 > 0) parts.Add(Below1000((int)(rupees % 1_000)));
        var rupeeText = rupees > 0 ? string.Join(" ", parts) + " Rupees" : "";
        var paiseText = paise > 0 ? Below100(paise) + " Paise" : "";
        var result = string.Join(" and ", new[] { rupeeText, paiseText }.Where(x => x.Length > 0));
        return result + " Only";
    }

    public static byte[] Render(JsonElement snapshot)
    {
        var sale = snapshot.GetProperty("sale");
        var business = snapshot.GetProperty("business");
        var customer = snapshot.GetProperty("customer");
        var lines = snapshot.GetProperty("lines");
        var gstValue = sale.GetProperty("gstRate");
        var hasGst = gstValue.ValueKind == JsonValueKind.Number;
        var gstRate = hasGst ? gstValue.GetDecimal() : 0m;
        var total = Number(sale, "total");
        var taxable = hasGst ? total / (1m + gstRate / 100m) : total;
        var tax = total - taxable;
        var paid = Number(sale, "amountPaid");

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(style => style.FontFamily("Lato", "Noto Sans Devanagari").FontSize(9));
                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(Text(business, "businessName", "Business")).FontSize(19).SemiBold();
                        var address = string.Join(", ", new[] { Text(business, "address"), Text(business, "city"), Text(business, "state"), Text(business, "pinCode") }.Where(x => !string.IsNullOrWhiteSpace(x)));
                        if (address.Length > 0) col.Item().Text(address);
                        var mobile = Text(business, "mobile");
                        if (mobile.Length > 0) col.Item().Text("Phone: " + mobile);
                        if (hasGst && Text(business, "gstin").Length > 0) col.Item().Text("GSTIN: " + Text(business, "gstin"));
                    });
                    row.ConstantItem(130).AlignRight().Column(col =>
                    {
                        col.Item().Text(hasGst ? "TAX INVOICE" : "INVOICE").FontSize(15).SemiBold();
                        col.Item().Text(Text(sale, "invoiceNo"));
                        col.Item().Text(Text(sale, "date"));
                    });
                });

                page.Content().PaddingTop(18).Column(col =>
                {
                    col.Item().BorderBottom(1).PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Column(p =>
                        {
                            p.Item().Text("Bill to").SemiBold();
                            p.Item().Text(Text(customer, "name", "Walk-in customer"));
                            var phone = Text(customer, "phone");
                            if (phone.Length > 0) p.Item().Text("Phone: " + phone);
                            var notes = string.Join(", ", Text(customer, "notes").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                            if (notes.Length > 0) p.Item().Text(notes);
                        });
                        row.RelativeItem().AlignRight().Column(p =>
                        {
                            p.Item().Text("Payment").SemiBold();
                            p.Item().Text(Text(sale, "payMethod", Text(sale, "saleType")));
                            p.Item().Text("Sold from: " + Text(snapshot, "location"));
                        });
                    });

                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(24);
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(1.25f);
                            columns.RelativeColumn(1.1f);
                            if (hasGst)
                            {
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                            }
                            columns.RelativeColumn(1.4f);
                        });
                        void Header(string label) => table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(label).SemiBold();
                        Header("#"); Header("Item"); Header("Qty"); Header("Rate"); Header("Discount");
                        if (hasGst) { Header("Taxable"); Header("CGST"); Header("SGST"); }
                        Header("Amount");
                        var index = 0;
                        foreach (var line in lines.EnumerateArray())
                        {
                            var net = Number(line, "lineTotal");
                            var lineTaxable = hasGst ? net / (1m + gstRate / 100m) : net;
                            var halfTax = (net - lineTaxable) / 2m;
                            void Cell(string value) => table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(value);
                            Cell((++index).ToString(CultureInfo.InvariantCulture));
                            var serials = line.TryGetProperty("serials", out var serialArray) && serialArray.ValueKind == JsonValueKind.Array
                                ? string.Join(", ", serialArray.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                                : "";
                            Cell(Text(line, "name") + (serials.Length > 0 ? "\\nSerial/IMEI: " + serials : ""));
                            Cell(Number(line, "quantity").ToString("0.###", CultureInfo.InvariantCulture));
                            Cell(Money(Number(line, "unitPrice")));
                            Cell(Money(Number(line, "discount")));
                            if (hasGst) { Cell(Money(lineTaxable)); Cell(Money(halfTax)); Cell(Money(halfTax)); }
                            Cell(Money(net));
                        }
                    });

                    col.Item().PaddingTop(12).AlignRight().Width(250).Column(totals =>
                    {
                        if (hasGst)
                        {
                            totals.Item().Row(r => { r.RelativeItem().Text("Taxable amount"); r.AutoItem().Text(Money(taxable)); });
                            totals.Item().Row(r => { r.RelativeItem().Text("CGST"); r.AutoItem().Text(Money(tax / 2m)); });
                            totals.Item().Row(r => { r.RelativeItem().Text("SGST"); r.AutoItem().Text(Money(tax / 2m)); });
                        }
                        totals.Item().PaddingTop(4).BorderTop(1).Row(r =>
                        {
                            r.RelativeItem().Text("Total").SemiBold();
                            r.AutoItem().Text(Money(total)).SemiBold();
                        });
                        if (paid < total)
                        {
                            totals.Item().Row(r => { r.RelativeItem().Text("Paid"); r.AutoItem().Text(Money(paid)); });
                            totals.Item().Row(r => { r.RelativeItem().Text("Balance due").SemiBold(); r.AutoItem().Text(Money(total - paid)).SemiBold(); });
                        }
                    });

                    col.Item().PaddingTop(10).Text("Amount in words: " + AmountInWords(total)).SemiBold();
                    col.Item().PaddingTop(24).Text("Goods once sold will not be taken back. Prices are inclusive of all taxes.");
                    col.Item().PaddingTop(28).AlignRight().Text("For " + Text(business, "businessName", "Business") + "\nAuthorised signatory");
                });
                page.Footer().AlignCenter().Text("Thank you for your business").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }
}
