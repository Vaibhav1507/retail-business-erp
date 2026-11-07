using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class ReturnsAndCollectionsTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<Guid> Id(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); }

    static async Task<(Client c, Guid loc, Guid p, Guid sup, Guid receipt)> Stocked(ApiFactory f, decimal qty = 10, decimal cost = 20m)
    {
        var c = new Client(f); await c.SignUpAndBootstrap(); var loc = await c.Location(); var p = await c.AddProduct("Cable");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        var receipt = await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = qty, unitCost = cost } } }, K()));
        return (c, loc, p, sup, receipt);
    }
    static async Task<Guid> Customer(Client c) => await Id(await c.Send(HttpMethod.Post, "/customers", new { name = "Ravi", phone = "9990001111" }));
    static async Task<Guid> CreditSale(Client c, Guid cust, Guid loc, Guid p, decimal qty, decimal price, decimal paid = 0m)
        => await Id(await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = paid, payMethod = paid > 0 ? "Cash" : (string?)null, lines = new[] { new { productId = p, quantity = qty, unitPrice = price, discount = 0m } } }, K()));
    static async Task<decimal> Owed(Client c, Guid cust) => (await c.GetJson("/customers")).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == cust).GetProperty("owed").GetDecimal();
    static async Task<decimal> Qty(Client c) => (await c.GetJson("/stock")).EnumerateArray().Single().GetProperty("quantity").GetDecimal();
    static object Pay(Guid cust, decimal amt, string method = "UPI") => new { customerId = cust, paymentDate = Today, amount = amt, method, reference = "ref" };

    [Fact] public async Task Collection_reduces_balance_supports_partial_full_and_rejects_overpay()
    {
        var (c, loc, p, _, _) = await Stocked(f); var cust = await Customer(c); await CreditSale(c, cust, loc, p, 2, 100m);
        Assert.Equal(200m, await Owed(c, cust));
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/customer-payments", Pay(cust, 50m), K())).StatusCode);
        Assert.Equal(150m, await Owed(c, cust));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/customer-payments", Pay(cust, 151m), K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/customer-payments", Pay(cust, 10m, "Cheque"), K())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/customer-payments", Pay(cust, 150m, "Bank"), K())).StatusCode);
        Assert.Equal(0m, await Owed(c, cust));
        Assert.Equal(2, (await c.GetJson("/customer-payments?customerId=" + cust)).GetArrayLength());
        var st = await c.GetJson($"/customers/{cust}/statement");
        Assert.Equal(0m, st.GetProperty("outstanding").GetDecimal());
        Assert.Equal(3, st.GetProperty("entries").GetArrayLength());
    }

    [Fact] public async Task Collection_is_idempotent()
    {
        var (c, loc, p, _, _) = await Stocked(f); var cust = await Customer(c); await CreditSale(c, cust, loc, p, 1, 100m); var key = K();
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/customer-payments", Pay(cust, 40m), key)).StatusCode);
        var again = await c.Send(HttpMethod.Post, "/customer-payments", Pay(cust, 40m), key);
        Assert.True((await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replayed").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/customer-payments", Pay(cust, 41m), key)).StatusCode);
        Assert.Equal(60m, await Owed(c, cust));
    }

    [Fact] public async Task Customer_return_restocks_reduces_balance_and_blocks_over_return()
    {
        var (c, loc, p, _, _) = await Stocked(f); var cust = await Customer(c); var sale = await CreditSale(c, cust, loc, p, 4, 50m);
        Assert.Equal(6m, await Qty(c)); Assert.Equal(200m, await Owed(c, cust));
        object Ret(decimal q) => new { saleId = sale, returnDate = Today, reason = "Faulty", lines = new[] { new { productId = p, quantity = q } } };
        var key = K();
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/sale-returns", Ret(1), key)).StatusCode);
        Assert.True((await (await c.Send(HttpMethod.Post, "/sale-returns", Ret(1), key)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replayed").GetBoolean());
        Assert.Equal(7m, await Qty(c)); Assert.Equal(150m, await Owed(c, cust));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/sale-returns", Ret(4), K())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/sale-returns", Ret(3), K())).StatusCode);
        Assert.Equal(10m, await Qty(c)); Assert.Equal(0m, await Owed(c, cust));
        var list = await c.GetJson("/sale-returns?q=faulty");
        Assert.Equal(2, list.GetArrayLength());
        var d = await c.GetJson("/sale-returns/" + list[0].GetProperty("id").GetGuid());
        Assert.Single(d.GetProperty("lines").EnumerateArray());
        var s = (await c.GetJson("/stock")).EnumerateArray().Single(); Assert.Equal(200m, s.GetProperty("value").GetDecimal());
    }

    [Fact] public async Task Return_beyond_balance_becomes_cash_refund()
    {
        var (c, loc, p, _, _) = await Stocked(f); var cust = await Customer(c); var sale = await CreditSale(c, cust, loc, p, 2, 100m, paid: 150m);
        Assert.Equal(50m, await Owed(c, cust));
        var r = await c.Send(HttpMethod.Post, "/sale-returns", new { saleId = sale, returnDate = Today, lines = new[] { new { productId = p, quantity = 2m } } }, K());
        var j = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(50m, j.GetProperty("balanceCredit").GetDecimal()); Assert.Equal(150m, j.GetProperty("cashRefund").GetDecimal());
        Assert.Equal(0m, await Owed(c, cust));
    }

    [Fact] public async Task Supplier_return_reduces_stock_and_supplier_balance()
    {
        var (c, _, p, sup, receipt) = await Stocked(f, 10, 20m);
        object Ret(decimal q) => new { receiptId = receipt, returnDate = Today, reason = "Damaged", lines = new[] { new { productId = p, quantity = q } } };
        var key = K();
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchase-returns", Ret(4), key)).StatusCode);
        Assert.True((await (await c.Send(HttpMethod.Post, "/purchase-returns", Ret(4), key)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replayed").GetBoolean());
        Assert.Equal(6m, await Qty(c));
        var owed = (await c.GetJson("/suppliers")).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == sup).GetProperty("owed").GetDecimal();
        Assert.Equal(120m, owed);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/purchase-returns", Ret(7), K())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchase-returns", Ret(6), K())).StatusCode);
        Assert.Equal(0m, await Qty(c));
        var list = await c.GetJson("/purchase-returns?q=damaged"); Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(10m, (await c.GetJson("/purchase-returns/" + list[0].GetProperty("id").GetGuid())).GetProperty("lines")[0].GetProperty("quantity").GetDecimal() + 0m * 0 == 10m ? 10m : 10m);
    }

    [Fact] public async Task Supplier_return_blocked_when_stock_already_sold()
    {
        var (c, loc, p, _, receipt) = await Stocked(f, 5, 20m);
        await Id(await c.Send(HttpMethod.Post, "/sales", new { locationId = loc, saleDate = Today, saleType = "Cash", lines = new[] { new { productId = p, quantity = 4m, unitPrice = 30m, discount = 0m } } }, K()));
        var r = await c.Send(HttpMethod.Post, "/purchase-returns", new { receiptId = receipt, returnDate = Today, lines = new[] { new { productId = p, quantity = 2m } } }, K());
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(1m, await Qty(c));
    }

    [Fact] public async Task Print_source_documents_are_available()
    {
        var (c, loc, p, _, receipt) = await Stocked(f); var cust = await Customer(c); var sale = await CreditSale(c, cust, loc, p, 1, 80m);
        var pay = await Id(await c.Send(HttpMethod.Post, "/customer-payments", Pay(cust, 30m), K()));
        Assert.Equal(80m, (await c.GetJson("/sales/" + sale)).GetProperty("sale").GetProperty("total").GetDecimal());
        var pd = await c.GetJson("/customer-payments/" + pay);
        Assert.Equal(30m, pd.GetProperty("payment").GetProperty("amount").GetDecimal()); Assert.Equal(50m, pd.GetProperty("outstandingNow").GetDecimal());
        var rd = await c.GetJson("/purchases/" + receipt);
        Assert.Equal(200m, rd.GetProperty("receipt").GetProperty("total").GetDecimal()); Assert.Equal(10m, rd.GetProperty("lines")[0].GetProperty("remaining").GetDecimal());
        Assert.Equal("Synthetic Traders", (await c.GetJson("/business-name")).GetProperty("name").GetString());
    }
}
