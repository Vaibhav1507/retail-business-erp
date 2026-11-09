using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class ReconciliationTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<Guid> Id(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); }

    async Task<(Client c, Guid loc, Guid p, Guid sup, Guid cust)> Setup()
    {
        var c = new Client(f); await c.SignUpAndBootstrap(); var loc = await c.Location(); var p = await c.AddProduct("Cable");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        var cust = await Id(await c.Send(HttpMethod.Post, "/customers", new { name = "Ravi", phone = "9990001111" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 10m, unitCost = 20m } } }, K()));
        return (c, loc, p, sup, cust);
    }

    static async Task<decimal> LedgerBalance(Client c, string code)
    {
        var line = (await c.GetJson("/reports/trial-balance")).GetProperty("lines").EnumerateArray().FirstOrDefault(l => l.GetProperty("code").GetString() == code);
        return line.ValueKind == JsonValueKind.Undefined ? 0m : line.GetProperty("debit").GetDecimal() - line.GetProperty("credit").GetDecimal();
    }

    static async Task<decimal> SumOf(Client c, string path, string field) => (await c.GetJson(path)).EnumerateArray().Sum(x => x.GetProperty(field).GetDecimal());

    static async Task AssertSubledgersMatchLedger(Client c)
    {
        var tb = await c.GetJson("/reports/trial-balance");
        Assert.Equal(tb.GetProperty("totalDebit").GetDecimal(), tb.GetProperty("totalCredit").GetDecimal());
        Assert.Equal(await SumOf(c, "/customers", "owed"), await LedgerBalance(c, "1200"));
        Assert.Equal(-await SumOf(c, "/suppliers", "owed"), await LedgerBalance(c, "2000"));
        Assert.Equal((await c.GetJson("/reports/stock-valuation")).GetProperty("totalValue").GetDecimal(), await LedgerBalance(c, "1400"));
    }

    [Fact] public async Task Receivables_payables_and_inventory_ledgers_match_their_subledgers_after_mixed_activity()
    {
        var (c, loc, p, sup, cust) = await Setup();
        await AssertSubledgersMatchLedger(c);
        await Id(await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 40m, payMethod = "Cash", lines = new[] { new { productId = p, quantity = 4m, unitPrice = 50m, discount = 0m } } }, K()));
        await AssertSubledgersMatchLedger(c);
        await Id(await c.Send(HttpMethod.Post, "/customer-payments", new { customerId = cust, paymentDate = Today, amount = 60m, method = "UPI", reference = "r" }, K()));
        await AssertSubledgersMatchLedger(c);
        await Id(await c.Send(HttpMethod.Post, "/supplier-payments", new { supplierId = sup, paymentDate = Today, amount = 50m, method = "UPI", reference = "r" }, K()));
        await AssertSubledgersMatchLedger(c);
    }

    [Fact] public async Task Rejected_sale_leaves_stock_balances_and_ledger_unchanged()
    {
        var (c, loc, p, _, cust) = await Setup();
        var before = (await c.GetJson("/reports/trial-balance")).ToString();
        var stockBefore = (await c.GetJson("/stock")).ToString();
        var bad = await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 0m, lines = new[] { new { productId = p, quantity = 11m, unitPrice = 50m, discount = 0m } } }, K());
        Assert.NotEqual(HttpStatusCode.OK, bad.StatusCode);
        Assert.Equal(before, (await c.GetJson("/reports/trial-balance")).ToString());
        Assert.Equal(stockBefore, (await c.GetJson("/stock")).ToString());
        Assert.Equal(0m, await SumOf(c, "/customers", "owed"));
    }

    [Fact] public async Task Rejected_supplier_payment_and_collection_leave_ledger_unchanged()
    {
        var (c, _, _, sup, cust) = await Setup();
        var before = (await c.GetJson("/reports/trial-balance")).ToString();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/supplier-payments", new { supplierId = sup, paymentDate = Today, amount = 999m, method = "UPI" }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/customer-payments", new { customerId = cust, paymentDate = Today, amount = 5m, method = "UPI" }, K())).StatusCode);
        Assert.Equal(before, (await c.GetJson("/reports/trial-balance")).ToString());
        await AssertSubledgersMatchLedger(c);
    }

    [Fact] public async Task Rejected_over_return_and_oversized_adjustment_leave_stock_and_ledger_unchanged()
    {
        var (c, loc, p, _, cust) = await Setup();
        var sale = await Id(await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 0m, lines = new[] { new { productId = p, quantity = 3m, unitPrice = 50m, discount = 0m } } }, K()));
        var tb = (await c.GetJson("/reports/trial-balance")).ToString();
        var stock = (await c.GetJson("/stock")).ToString();

        var overReturn = await c.Send(HttpMethod.Post, "/sale-returns", new { saleId = sale, returnDate = Today, reason = "Faulty", lines = new[] { new { productId = p, quantity = 4m } } }, K());
        Assert.NotEqual(HttpStatusCode.OK, overReturn.StatusCode);
        var tooMany = await c.Send(HttpMethod.Post, "/stock-adjustments", new { productId = p, locationId = loc, date = Today, quantity = 100m, direction = "Decrease", reason = "Damage" }, K());
        Assert.NotEqual(HttpStatusCode.OK, tooMany.StatusCode);

        Assert.Equal(tb, (await c.GetJson("/reports/trial-balance")).ToString());
        Assert.Equal(stock, (await c.GetJson("/stock")).ToString());
        await AssertSubledgersMatchLedger(c);
    }

    [Fact] public async Task Cancelled_sale_restores_stock_ledger_and_customer_balance_and_cannot_repeat()
    {
        var (c, loc, p, _, cust) = await Setup();
        var tb = (await c.GetJson("/reports/trial-balance")).ToString();
        var stock = (await c.GetJson("/stock")).ToString();
        var sale = await Id(await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 20m, payMethod = "Cash", lines = new[] { new { productId = p, quantity = 3m, unitPrice = 50m, discount = 0m } } }, K()));
        Assert.Equal(130m, await SumOf(c, "/customers", "owed"));

        var key = K();
        var cancel = await c.Send(HttpMethod.Post, $"/sales/{sale}/cancel", new { reason = "Entered by mistake" }, key);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(0m, await SumOf(c, "/customers", "owed"));
        Assert.Equal(stock, (await c.GetJson("/stock")).ToString());
        await AssertSubledgersMatchLedger(c);
        Assert.Equal(0m, await LedgerBalance(c, "4000"));
        Assert.Equal(0m, await LedgerBalance(c, "5000"));

        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, $"/sales/{sale}/cancel", new { reason = "Entered by mistake" }, key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, $"/sales/{sale}/cancel", new { reason = "again" }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/sale-returns", new { saleId = sale, returnDate = Today, lines = new[] { new { productId = p, quantity = 1m } } }, K())).StatusCode);
        await AssertSubledgersMatchLedger(c);
    }

    [Fact] public async Task Sale_with_a_return_cannot_be_cancelled()
    {
        var (c, loc, p, _, cust) = await Setup();
        var sale = await Id(await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 0m, lines = new[] { new { productId = p, quantity = 3m, unitPrice = 50m, discount = 0m } } }, K()));
        await Id(await c.Send(HttpMethod.Post, "/sale-returns", new { saleId = sale, returnDate = Today, lines = new[] { new { productId = p, quantity = 1m } } }, K()));
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, $"/sales/{sale}/cancel", new { }, K())).StatusCode);
        await AssertSubledgersMatchLedger(c);
    }

    [Fact] public async Task Customer_payment_allocates_to_specific_sale_and_rejects_over_allocation()
    {
        var (c, loc, p, _, cust) = await Setup();
        object Body() => new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 0m, payMethod = "Cash", lines = new[] { new { productId = p, quantity = 1m, unitPrice = 50m, discount = 0m } } };
        var sale = await Id(await c.Send(HttpMethod.Post, "/sales", Body(), K()));
        var open = (await c.GetJson($"/customers/{cust}/open-sales")).EnumerateArray().Single();
        Assert.Equal(sale, open.GetProperty("id").GetGuid());
        object Pay(decimal amt, decimal alloc) => new { customerId = cust, paymentDate = Today, amount = amt, method = "Cash", allocations = new[] { new { billId = sale, amount = alloc } } };
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/customer-payments", Pay(10m, open.GetProperty("open").GetDecimal() + 1m), K())).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/customer-payments", Pay(10m, 10m), K())).StatusCode);
        Assert.Equal(open.GetProperty("open").GetDecimal() - 10m, (await c.GetJson($"/customers/{cust}/open-sales")).EnumerateArray().Single().GetProperty("open").GetDecimal());
    }

    [Fact] public async Task Invoice_numbers_are_sequential_and_replay_does_not_consume_one()
    {
        var (c, loc, p, _, cust) = await Setup();
        object Body() => new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 0m, payMethod = "Cash", lines = new[] { new { productId = p, quantity = 1m, unitPrice = 50m, discount = 0m } } };
        var k1 = K();
        var a = await Id(await c.Send(HttpMethod.Post, "/sales", Body(), k1));
        await c.Send(HttpMethod.Post, "/sales", Body(), k1);
        var b = await Id(await c.Send(HttpMethod.Post, "/sales", Body(), K()));
        var na = (await c.GetJson($"/sales/{a}")).GetProperty("sale").GetProperty("invoiceNo").GetString();
        var nb = (await c.GetJson($"/sales/{b}")).GetProperty("sale").GetProperty("invoiceNo").GetString();
        Assert.Matches(@"^INV/\d{4}-\d{2}/000001$", na!);
        Assert.Equal(na![..^1] + "2", nb);
    }

    [Fact] public async Task Another_business_report_totals_are_independent()
    {
        var (a, _, _, _, _) = await Setup();
        var b = new Client(f); await b.SignUpAndBootstrap();
        var tb = await b.GetJson("/reports/trial-balance");
        Assert.Equal(0, tb.GetProperty("lines").GetArrayLength());
        Assert.NotEqual(0, (await a.GetJson("/reports/trial-balance")).GetProperty("lines").GetArrayLength());
    }
}
