using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Rbos.IntegrationTests;

public class SalesToCashTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<(Client c, Guid loc, Guid p)> Stocked(ApiFactory f, decimal qty = 10, decimal cost = 20m)
    {
        var c = new Client(f); await c.SignUpAndBootstrap(); var loc = await c.Location(); var p = await c.AddProduct("Cable");
        var s = await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" });
        var sup = (await s.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var r = await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = qty, unitCost = cost } } }, K());
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return (c, loc, p);
    }
    static async Task<Guid> Customer(Client c, string name = "Ravi", string? notes = null)
        => (await (await c.Send(HttpMethod.Post, "/customers", new { name, phone = "9990001111", notes })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    static object Sale(Guid? cust, Guid loc, Guid p, decimal qty, decimal price, string type, decimal? paid = null, decimal discount = 0m, decimal? gstRate = null)
        => new { customerId = cust, locationId = loc, saleDate = Today, saleType = type, amountPaid = paid, payMethod = type == "Credit" ? "Cash" : (string?)null, gstRate, lines = new[] { new { productId = p, quantity = qty, unitPrice = price, discount } } };
    static async Task<JsonElement> Stock(Client c) => (await c.GetJson("/stock")).EnumerateArray().Single();
    static async Task<decimal> Owed(Client c, Guid cust) => (await c.GetJson("/customers")).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == cust).GetProperty("owed").GetDecimal();

    [Fact] public async Task Walk_in_cash_sale_deducts_stock_at_wac()
    {
        var (c, loc, p) = await Stocked(f);
        var r = await c.Send(HttpMethod.Post, "/sales", Sale(null, loc, p, 3, 50m, "Cash", discount: 10m), K());
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(140m, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetDecimal());
        var s = await Stock(c);
        Assert.Equal(7m, s.GetProperty("quantity").GetDecimal());
        Assert.Equal(20m, s.GetProperty("wac").GetDecimal());
        Assert.Equal(140m, s.GetProperty("value").GetDecimal());
    }
    [Fact] public async Task Credit_sale_with_part_payment_updates_customer_balance()
    {
        var (c, loc, p) = await Stocked(f); var cust = await Customer(c);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/sales", Sale(cust, loc, p, 2, 100m, "Credit", 60m), K())).StatusCode);
        Assert.Equal(140m, await Owed(c, cust));
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/sales", Sale(cust, loc, p, 1, 50m, "Credit"), K())).StatusCode);
        Assert.Equal(190m, await Owed(c, cust));
        Assert.Equal(7m, (await Stock(c)).GetProperty("quantity").GetDecimal());
    }
    [Fact] public async Task Credit_sale_requires_customer_and_overpay_rejected()
    {
        var (c, loc, p) = await Stocked(f); var cust = await Customer(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/sales", Sale(null, loc, p, 1, 50m, "Credit"), K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/sales", Sale(cust, loc, p, 1, 50m, "Credit", 60m), K())).StatusCode);
        Assert.Equal(10m, (await Stock(c)).GetProperty("quantity").GetDecimal());
    }
    [Fact] public async Task Sale_cannot_make_stock_negative()
    {
        var (c, loc, p) = await Stocked(f, 5);
        var r = await c.Send(HttpMethod.Post, "/sales", Sale(null, loc, p, 6, 50m, "UPI"), K());
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(5m, (await Stock(c)).GetProperty("quantity").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/sales", Sale(null, loc, p, 5, 50m, "UPI"), K())).StatusCode);
        var s = await Stock(c); Assert.Equal(0m, s.GetProperty("quantity").GetDecimal()); Assert.Equal(0m, s.GetProperty("value").GetDecimal());
    }
    [Fact] public async Task Duplicate_submission_is_safe_and_key_reuse_conflicts()
    {
        var (c, loc, p) = await Stocked(f); var key = K();
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/sales", Sale(null, loc, p, 2, 50m, "Cash"), key)).StatusCode);
        var again = await c.Send(HttpMethod.Post, "/sales", Sale(null, loc, p, 2, 50m, "Cash"), key);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.True((await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replayed").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, "/sales", Sale(null, loc, p, 3, 50m, "Cash"), key)).StatusCode);
        Assert.Equal(8m, (await Stock(c)).GetProperty("quantity").GetDecimal());
        Assert.Single((await c.GetJson("/sales")).EnumerateArray());
    }
    [Fact] public async Task Sale_detail_and_search_work()
    {
        var (c, loc, p) = await Stocked(f); var cust = await Customer(c, "Meera Stores", "12 Market Road | Bhagalpur | Bihar | 812001");
        var id = (await (await c.Send(HttpMethod.Post, "/sales", Sale(cust, loc, p, 1, 80m, "Credit", 30m, gstRate: 18m), K())).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var d = await c.GetJson("/sales/" + id);
        Assert.Equal(50m, d.GetProperty("sale").GetProperty("balance").GetDecimal());
        Assert.Equal(18m, d.GetProperty("sale").GetProperty("gstRate").GetDecimal());
        Assert.Equal("12 Market Road | Bhagalpur | Bihar | 812001", d.GetProperty("sale").GetProperty("customerNotes").GetString());
        Assert.Single(d.GetProperty("lines").EnumerateArray());
        Assert.Single((await c.GetJson("/sales?q=meera")).EnumerateArray());
        Assert.Empty((await c.GetJson("/sales?q=nobody")).EnumerateArray());
        Assert.Single((await c.GetJson("/customers?q=meera")).EnumerateArray());
    }
    [Fact] public async Task Other_business_cannot_see_or_sell_against_data()
    {
        var (a, loc, p) = await Stocked(f); var cust = await Customer(a);
        await a.Send(HttpMethod.Post, "/sales", Sale(cust, loc, p, 1, 50m, "Credit"), K());
        var b = new Client(f); await b.SignUpAndBootstrap();
        Assert.Empty((await b.GetJson("/customers")).EnumerateArray());
        Assert.Empty((await b.GetJson("/sales")).EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Send(HttpMethod.Post, "/sales", Sale(cust, await b.Location(), p, 1, 50m, "Credit"), K())).StatusCode);
    }
    [Fact] public async Task Invoice_snapshot_and_delivery_respect_explicit_customer_consent()
    {
        var (c, loc, p) = await Stocked(f);
        var created = await c.Send(HttpMethod.Post, "/customers", new { name = "Opted In", phone = "9876543210", whatsappConsentStatus = "OptedIn" });
        var customer = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var consentAudit = await c.GetJson("/audit?q=" + Uri.EscapeDataString("WhatsApp invoice consent"));
        Assert.Contains(consentAudit.EnumerateArray(), entry => entry.GetProperty("action").GetString() == "CustomerWhatsAppConsentChanged" && entry.GetProperty("entityId").GetGuid() == customer);
        var response = await c.Send(HttpMethod.Post, "/sales", Sale(customer, loc, p, 1, 100m, "Cash"), K());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var detail = await c.GetJson("/sales/" + id);
        Assert.Equal("Pending", detail.GetProperty("sale").GetProperty("whatsappStatus").GetString());

        // Checkout's ordinary customer update omits the consent field; it must preserve the customer's permission.
        await c.Send(HttpMethod.Put, "/customers/" + customer, new { name = "Opted In Updated", phone = "9876543210", notes = (string?)null, isActive = true });
        var listed = (await c.GetJson("/customers")).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == customer);
        Assert.Equal("OptedIn", listed.GetProperty("whatsappConsentStatus").GetString());

        // A changed phone is a new WhatsApp recipient. Permission must be collected again.
        await c.Send(HttpMethod.Put, "/customers/" + customer, new { name = "Opted In Updated", phone = "9876543212", notes = (string?)null, isActive = true });
        var changedPhone = (await c.GetJson("/customers")).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == customer);
        Assert.Equal("Unknown", changedPhone.GetProperty("whatsappConsentStatus").GetString());
    }

    [Fact] public async Task Unknown_or_opted_out_consent_never_queues_whatsapp_delivery()
    {
        var (c, loc, p) = await Stocked(f);
        var unknown = await Customer(c, "Unknown Consent");
        var unknownSale = (await (await c.Send(HttpMethod.Post, "/sales", Sale(unknown, loc, p, 1, 50m, "Cash"), K())).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal("ConsentRequired", (await c.GetJson("/sales/" + unknownSale)).GetProperty("sale").GetProperty("whatsappStatus").GetString());

        // Later permission is explicit, and a changed phone becomes the retry recipient.
        await c.Send(HttpMethod.Put, "/customers/" + unknown,
            new { name = "Unknown Consent", phone = "9876543213", notes = (string?)null, isActive = true, whatsappConsentStatus = "OptedIn" });
        var retry = await c.Send(HttpMethod.Post, "/sales/" + unknownSale + "/invoice/whatsapp/retry", new { });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("Pending", (await c.GetJson("/sales/" + unknownSale)).GetProperty("sale").GetProperty("whatsappStatus").GetString());

        var optedOutResponse = await c.Send(HttpMethod.Post, "/customers", new { name = "Opted Out", phone = "9876543211", whatsappConsentStatus = "OptedOut" });
        var optedOut = (await optedOutResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var optedOutSale = (await (await c.Send(HttpMethod.Post, "/sales", Sale(optedOut, loc, p, 1, 50m, "Cash"), K())).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal("Suppressed", (await c.GetJson("/sales/" + optedOutSale)).GetProperty("sale").GetProperty("whatsappStatus").GetString());
    }

    [Fact] public async Task WhatsApp_dispatch_queue_lease_is_exclusive_and_can_be_rescheduled()
    {
        var (c, loc, p) = await Stocked(f);
        var customerResponse = await c.Send(HttpMethod.Post, "/customers",
            new { name = "Queue Test", phone = "9876543210", whatsappConsentStatus = "OptedIn" });
        var customer = (await customerResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var saleResponse = await c.Send(HttpMethod.Post, "/sales", Sale(customer, loc, p, 1, 100m, "Cash"), K());
        Assert.Equal(HttpStatusCode.OK, saleResponse.StatusCode);

        var config = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        var workerConnectionString = config["ConnectionStrings:RbosTestWhatsAppWorker"]!;
        await using var worker = new NpgsqlConnection(workerConnectionString);
        await worker.OpenAsync();

        async Task<(Guid DeliveryId, Guid LeaseToken, int AttemptCount)?> Claim()
        {
            await using var command = new NpgsqlCommand(
                "select delivery_id, lease_token, attempt_count from integrations.claim_invoice_deliveries(5,30)", worker);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return (reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2));
        }

        var first = await Claim();
        Assert.NotNull(first);
        Assert.Equal(1, first.Value.AttemptCount);
        Assert.Null(await Claim()); // A live lease prevents a second worker from claiming the same delivery.

        await using (var reschedule = new NpgsqlCommand(
            "select integrations.finish_invoice_delivery(@delivery,@lease,@retry)", worker))
        {
            reschedule.Parameters.AddWithValue("delivery", first.Value.DeliveryId);
            reschedule.Parameters.AddWithValue("lease", first.Value.LeaseToken);
            reschedule.Parameters.AddWithValue("retry", DateTimeOffset.UtcNow.AddSeconds(-1));
            Assert.True((bool)(await reschedule.ExecuteScalarAsync())!);
        }

        var second = await Claim();
        Assert.NotNull(second);
        Assert.Equal(2, second.Value.AttemptCount);
        Assert.Equal(first.Value.DeliveryId, second.Value.DeliveryId);

        await using (var finish = new NpgsqlCommand(
            "select integrations.finish_invoice_delivery(@delivery,@lease,null)", worker))
        {
            finish.Parameters.AddWithValue("delivery", second.Value.DeliveryId);
            finish.Parameters.AddWithValue("lease", second.Value.LeaseToken);
            Assert.True((bool)(await finish.ExecuteScalarAsync())!);
        }
        Assert.Null(await Claim());
    }

}
