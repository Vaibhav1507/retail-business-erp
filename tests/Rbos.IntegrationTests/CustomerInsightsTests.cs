using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class CustomerInsightsTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<Guid> Id(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); }

    [Fact]
    public async Task History_lists_sales_and_top_products_and_ageing_reflects_dues_and_payments()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location(); var p = await c.AddProduct("Cable");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        var cust = await Id(await c.Send(HttpMethod.Post, "/customers", new { name = "Ravi", phone = "9990001111" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 20m, unitCost = 20m } } }, K()));
        object Sale(decimal qty, decimal paid) => new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = paid, payMethod = "Cash", lines = new[] { new { productId = p, quantity = qty, unitPrice = 50m, discount = 0m } } };
        await Id(await c.Send(HttpMethod.Post, "/sales", Sale(4m, 0m), K()));
        var second = await Id(await c.Send(HttpMethod.Post, "/sales", Sale(2m, 0m), K()));
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, $"/sales/{second}/cancel", new { reason = "Mistake" }, K())).StatusCode);

        var h = await c.GetJson($"/customers/{cust}/history");
        Assert.Equal("Ravi", h.GetProperty("customer").GetString());
        Assert.Equal(2, h.GetProperty("sales").GetArrayLength());
        var top = h.GetProperty("topProducts");
        Assert.Equal(1, top.GetArrayLength());
        Assert.Equal(4m, top[0].GetProperty("quantity").GetDecimal());

        var ageing = await c.GetJson("/customers/ageing");
        Assert.Equal(1, ageing.GetArrayLength());
        Assert.Equal(200m, ageing[0].GetProperty("outstanding").GetDecimal());
        Assert.Equal(200m, ageing[0].GetProperty("buckets")[0].GetProperty("amount").GetDecimal());

        await Id(await c.Send(HttpMethod.Post, "/customer-payments", new { customerId = cust, paymentDate = Today, amount = 200m, method = "UPI", reference = "r" }, K()));
        Assert.Equal(0, (await c.GetJson("/customers/ageing")).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await c.Send(HttpMethod.Get, $"/customers/{Guid.NewGuid()}/history", null)).StatusCode);
    }
}

public class SplitPaymentTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<Guid> Id(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); }

    async Task<(Client c, Guid loc, Guid p, Guid cust)> Setup()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location(); var p = await c.AddProduct("Cable");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        var cust = await Id(await c.Send(HttpMethod.Post, "/customers", new { name = "Ravi", phone = "9990001111" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 20m, unitCost = 20m } } }, K()));
        return (c, loc, p, cust);
    }

    static object Body(Guid cust, Guid loc, Guid p, string type, object payments) =>
        new { customerId = cust, locationId = loc, saleDate = Today, saleType = type, payments, lines = new[] { new { productId = p, quantity = 4m, unitPrice = 50m, discount = 0m } } };

    [Fact]
    public async Task Split_credit_sale_leaves_remainder_due_and_replays_idempotently()
    {
        var (c, loc, p, cust) = await Setup();
        var key = K();
        var body = Body(cust, loc, p, "Credit", new[] { new { method = "Cash", amount = 50m }, new { method = "UPI", amount = 100m } });
        var id = await Id(await c.Send(HttpMethod.Post, "/sales", body, key));
        Assert.Equal(id, await Id(await c.Send(HttpMethod.Post, "/sales", body, key)));
        var ageing = await c.GetJson("/customers/ageing");
        Assert.Equal(1, ageing.GetArrayLength());
        Assert.Equal(50m, ageing[0].GetProperty("outstanding").GetDecimal());
    }

    [Fact]
    public async Task Fully_paid_cash_sale_can_be_split_across_methods()
    {
        var (c, loc, p, cust) = await Setup();
        var body = Body(cust, loc, p, "Cash", new[] { new { method = "Cash", amount = 120m }, new { method = "Bank", amount = 80m } });
        await Id(await c.Send(HttpMethod.Post, "/sales", body, K()));
        Assert.Equal(0, (await c.GetJson("/customers/ageing")).GetArrayLength());
    }

    [Fact]
    public async Task Invalid_splits_are_rejected()
    {
        var (c, loc, p, cust) = await Setup();
        foreach (var (type, pay) in new (string, object)[]
        {
            ("Credit", new[] { new { method = "Cash", amount = 10m }, new { method = "Cash", amount = 20m } }),
            ("Credit", new[] { new { method = "Cheque", amount = 10m } }),
            ("Credit", new[] { new { method = "Cash", amount = 250m } }),
            ("Cash", new[] { new { method = "Cash", amount = 100m } }),
        })
        {
            var r = await c.Send(HttpMethod.Post, "/sales", Body(cust, loc, p, type, pay), K());
            Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        }
    }
}

public class ReturnRefundMethodTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<Guid> Id(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); }

    [Fact]
    public async Task Refund_method_is_validated_and_stored_for_cash_refunds()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location(); var p = await c.AddProduct("Cable");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 10m, unitCost = 20m } } }, K()));
        var sale = await Id(await c.Send(HttpMethod.Post, "/sales", new { locationId = loc, saleDate = Today, saleType = "Cash", lines = new[] { new { productId = p, quantity = 4m, unitPrice = 50m, discount = 0m } } }, K()));
        object Ret(string? method, decimal qty) => new { saleId = sale, returnDate = Today, reason = "Faulty", refundMethod = method, lines = new[] { new { productId = p, quantity = qty } } };

        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/sale-returns", Ret("Cheque", 1m), K())).StatusCode);
        var ok = await Id(await c.Send(HttpMethod.Post, "/sale-returns", Ret("UPI", 1m), K()));
        var list = await c.GetJson("/sale-returns");
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(50m, list[0].GetProperty("cashRefund").GetDecimal());
        Assert.NotEqual(Guid.Empty, ok);
    }
}
