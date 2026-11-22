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
    [Fact] public async Task Insufficient_stock_sale_uses_provisional_cost_and_receipt_reconciles_it()
    {
        var (c, loc, p) = await Stocked(f, 5, 20m);
        var saleResponse = await c.Send(HttpMethod.Post, "/sales", Sale(null, loc, p, 6, 50m, "UPI"), K());
        Assert.Equal(HttpStatusCode.OK, saleResponse.StatusCode);
        var saleId = (await saleResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var afterSale = await Stock(c);
        Assert.Equal(-1m, afterSale.GetProperty("quantity").GetDecimal());
        Assert.Equal(0m, afterSale.GetProperty("value").GetDecimal());

        var supplierResponse = await c.Send(HttpMethod.Post, "/suppliers", new { name = "Settlement supplier" });
        var supplierId = (await supplierResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var receipt = await c.Send(HttpMethod.Post, "/purchases", new
        {
            supplierId, locationId = loc, receiptDate = Today,
            lines = new[] { new { productId = p, quantity = 1m, unitCost = 30m } }
        }, K());
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        var settled = await Stock(c);
        Assert.Equal(0m, settled.GetProperty("quantity").GetDecimal());
        Assert.Equal(0m, settled.GetProperty("value").GetDecimal());

        var saleDetail = await c.GetJson("/sales/" + saleId);
        Assert.Equal(6m, saleDetail.GetProperty("lines")[0].GetProperty("quantity").GetDecimal());
        var returnable = (await c.GetJson("/sales/" + saleId + "/returnable")).EnumerateArray().Single();
        var returnResponse = await c.Send(HttpMethod.Post, "/sale-returns", new
        {
            saleId, returnDate = Today, reason = "Return after settlement",
            lines = new[] { new { saleLineId = returnable.GetProperty("saleLineId").GetGuid(), productId = p, quantity = 1m } },
            refundMethod = "Cash"
        }, K());
        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);
        var afterReturn = await Stock(c);
        Assert.Equal(1m, afterReturn.GetProperty("quantity").GetDecimal());
        Assert.True(afterReturn.GetProperty("value").GetDecimal() > 0m);
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
    [Fact] public async Task Sale_search_matches_serial_case_insensitively_and_preserves_original_value()
    {
        var (c, loc, p) = await Stocked(f);
        const string serial = "abC-01oI";
        var response = await c.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null, locationId = loc, saleDate = Today, saleType = "Cash",
            amountPaid = (decimal?)null, payMethod = (string?)null,
            lines = new[] { new { productId = p, quantity = 1m, unitPrice = 80m, discount = 0m, serials = new[] { serial } } }
        }, K());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saleId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var detail = await c.GetJson("/sales/" + saleId);
        Assert.Equal(serial, detail.GetProperty("lines")[0].GetProperty("serials")[0].GetString());
        Assert.Single((await c.GetJson("/sales?q=ABC-01OI")).EnumerateArray());
        Assert.Single((await c.GetJson("/sales?q=abC-01oI")).EnumerateArray());
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
        await using var transaction = await worker.BeginTransactionAsync();

        async Task<(Guid DeliveryId, Guid LeaseToken, int AttemptCount)?> Claim()
        {
            await using var command = new NpgsqlCommand(
                "select delivery_id, lease_token, attempt_count from integrations.claim_invoice_deliveries(25,30)", worker, transaction);
            await using var reader = await command.ExecuteReaderAsync();
            var claimed = new List<(Guid DeliveryId, Guid LeaseToken, int AttemptCount)>();
            while (await reader.ReadAsync())
                claimed.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2)));
            return claimed.Count == 0 ? null : claimed[0];
        }

        var first = await Claim();
        Assert.NotNull(first);
        Assert.Equal(1, first.Value.AttemptCount);
        Assert.Null(await Claim()); // A live lease prevents a second worker from claiming the same delivery.

        await using (var reschedule = new NpgsqlCommand(
            "select integrations.finish_invoice_delivery(@delivery,@lease,@retry)", worker, transaction))
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
            "select integrations.finish_invoice_delivery(@delivery,@lease,null)", worker, transaction))
        {
            finish.Parameters.AddWithValue("delivery", second.Value.DeliveryId);
            finish.Parameters.AddWithValue("lease", second.Value.LeaseToken);
            Assert.True((bool)(await finish.ExecuteScalarAsync())!);
        }
        Assert.Null(await Claim());
        await transaction.RollbackAsync(); // Keep the shared CI queue unchanged for other tests.
    }


    [Fact] public async Task Scanned_barcode_is_learned_only_after_successful_invoice_and_cannot_be_reassigned()
    {
        var (client, location, product) = await Stocked(f, qty: 5);
        const string code = "4006381333931";
        var failed = await client.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null, locationId = location, saleDate = Today, saleType = "Cash",
            amountPaid = (decimal?)null, payMethod = (string?)null,
            lines = new[] { new
            {
                productId = product, quantity = 1m, unitPrice = 0m, discount = 0m,
                serials = Array.Empty<string>(), barcode = code, barcodeSymbology = "EAN_13"
            } }
        }, K());
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.Raw("/products/by-barcode?code=" + code)).StatusCode);

        var posted = await client.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null, locationId = location, saleDate = Today, saleType = "Cash",
            amountPaid = (decimal?)null, payMethod = (string?)null,
            lines = new[] { new
            {
                productId = product, quantity = 1m, unitPrice = 100m, discount = 0m,
                serials = new[] { "SERIAL12345" }, barcode = code, barcodeSymbology = "EAN_13"
            } }
        }, K());
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);

        var lookup = await client.GetJson("/products/by-barcode?code=" + code);
        Assert.Equal(product, lookup.GetProperty("productId").GetGuid());

        var otherProduct = await client.AddProduct("Different product cannot claim barcode");
        var conflict = await client.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null, locationId = location, saleDate = Today, saleType = "Cash",
            amountPaid = (decimal?)null, payMethod = (string?)null,
            lines = new[] { new
            {
                productId = otherProduct, quantity = 1m, unitPrice = 100m, discount = 0m,
                serials = Array.Empty<string>(), barcode = code, barcodeSymbology = "EAN_13"
            } }
        }, K());
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(product, (await client.GetJson("/products/by-barcode?code=" + code))
            .GetProperty("productId").GetGuid());
    }

    [Fact] public async Task Verified_barcode_mapping_is_reused_by_another_business_in_the_same_vertical()
    {
        static async Task SelectElectronicsVertical(Client client, string name)
        {
            var response = await client.Send(HttpMethod.Put, "/business-profile", new
            {
                businessName = name, businessCategory = "electronics_mobile",
                ownerName = "Shop Owner", mobile = (string?)null,
                address = "1 Market Road", city = "Bengaluru", state = "Karnataka",
                pinCode = "560001", gstStatus = "NotRegistered", gstin = (string?)null,
                financialYearStartMonth = 4, language = "en"
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var first = new Client(f);
        await first.SignUpAndBootstrap();
        await SelectElectronicsVertical(first, "Electronics Shop A");
        var firstProduct = await first.AddProduct("Shared Electronics Television Model X");
        var location = await first.Location();
        var supplierResponse = await first.Send(HttpMethod.Post, "/suppliers", new { name = "Shared Supplier A" });
        Assert.Equal(HttpStatusCode.OK, supplierResponse.StatusCode);
        var supplier = (await supplierResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var receipt = await first.Send(HttpMethod.Post, "/purchases", new
        {
            supplierId = supplier, locationId = location, receiptDate = Today,
            lines = new[] { new { productId = firstProduct, quantity = 3m, unitCost = 1000m } }
        }, K());
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);

        const string code = "4006381333931";
        var sale = await first.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null, locationId = location, saleDate = Today, saleType = "Cash",
            amountPaid = (decimal?)null, payMethod = (string?)null,
            lines = new[] { new
            {
                productId = firstProduct, quantity = 1m, unitPrice = 1500m, discount = 0m,
                serials = Array.Empty<string>(), barcode = code, barcodeSymbology = "EAN_13"
            } }
        }, K());
        Assert.Equal(HttpStatusCode.OK, sale.StatusCode);

        var second = new Client(f);
        await second.SignUpAndBootstrap();
        await SelectElectronicsVertical(second, "Electronics Shop B");
        var sharedLookup = await second.GetJson("/products/by-barcode?code=" + code);
        Assert.Equal("Shared Electronics Television Model X", sharedLookup.GetProperty("name").GetString());
        Assert.Equal(code, sharedLookup.GetProperty("code").GetString());

        // A second lookup now uses the tenant's own materialized map, not AI or an external provider.
        var secondLookup = await second.GetJson("/products/by-barcode?code=" + code);
        Assert.Equal(sharedLookup.GetProperty("productId").GetGuid(), secondLookup.GetProperty("productId").GetGuid());
    }

    [Fact] public async Task Dual_sim_phone_keeps_two_imeis_separate_from_one_physical_serial()
    {
        var (client, location, product) = await Stocked(f, qty: 2);
        const string serial = "SERIAL12345";
        const string imei1 = "490154203237518";
        const string imei2 = "490154203237526";
        var response = await client.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null, locationId = location, saleDate = Today, saleType = "Cash",
            amountPaid = (decimal?)null, payMethod = (string?)null,
            lines = new[] { new
            {
                productId = product, quantity = 1m, unitPrice = 1000m, discount = 0m,
                serials = new[] { serial }, imei1s = new[] { imei1 }, imei2s = new[] { imei2 }
            } }
        }, K());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saleId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var detail = await client.GetJson("/sales/" + saleId);
        var line = detail.GetProperty("lines")[0];
        Assert.Equal(new[] { serial }, line.GetProperty("serials").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(new[] { imei1 }, line.GetProperty("imei1s").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(new[] { imei2 }, line.GetProperty("imei2s").EnumerateArray().Select(x => x.GetString()).ToArray());
    }
    [Fact] public async Task Ad_hoc_and_repeated_product_lines_post_without_catalogue_creation_and_return_by_line_id()
    {
        var (c, loc, p) = await Stocked(f, qty: 10, cost: 20m);
        var productsBefore = (await c.GetJson("/products")).EnumerateArray().Count();
        var response = await c.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null,
            locationId = loc,
            saleDate = Today,
            saleType = "Cash",
            lines = new object[]
            {
                new { productId = p, description = "catalogue row one", quantity = 1m, unitPrice = 100m, discount = 0m },
                new { productId = p, description = "catalogue row two", quantity = 1m, unitPrice = 95m, discount = 5m },
                new { productId = (Guid?)null, description = "Unlisted charger model ZX-90", model = "ZX-90", brand = "Local", hsnCode = "850440", quantity = 1m, unitPrice = 250m, discount = 0m }
            }
        }, K());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saleId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var detail = await c.GetJson("/sales/" + saleId);
        var saleLines = detail.GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(3, saleLines.Length);
        var adHoc = Assert.Single(saleLines, x => x.GetProperty("product").GetString() == "Unlisted charger model ZX-90");
        Assert.Equal("Unlisted charger model ZX-90", adHoc.GetProperty("product").GetString());
        Assert.Equal("ZX-90", adHoc.GetProperty("model").GetString());
        Assert.Equal(productsBefore, (await c.GetJson("/products")).EnumerateArray().Count());
        var stock = await Stock(c);
        Assert.Equal(8m, stock.GetProperty("quantity").GetDecimal());
        Assert.Equal(160m, stock.GetProperty("value").GetDecimal());

        var returnable = (await c.GetJson("/sales/" + saleId + "/returnable")).EnumerateArray().ToArray();
        var adHocReturn = Assert.Single(returnable.Where(x => x.GetProperty("productId").ValueKind == JsonValueKind.Null));
        var returnResponse = await c.Send(HttpMethod.Post, "/sale-returns", new
        {
            saleId,
            returnDate = Today,
            reason = "Customer returned unlisted item",
            lines = new[] { new { saleLineId = adHocReturn.GetProperty("saleLineId").GetGuid(), productId = (Guid?)null, quantity = 1m } },
            refundMethod = "Cash"
        }, K());
        Assert.Equal(HttpStatusCode.OK, returnResponse.StatusCode);
        Assert.Equal(8m, (await Stock(c)).GetProperty("quantity").GetDecimal());
        var postedReturn = (await returnResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var returnDetail = await c.GetJson("/sale-returns/" + postedReturn);
        Assert.Equal("Unlisted charger model ZX-90", returnDetail.GetProperty("lines")[0].GetProperty("product").GetString());

        var catalogueLine = returnable.First(x => x.GetProperty("productId").ValueKind != JsonValueKind.Null);
        var catalogueReturn = await c.Send(HttpMethod.Post, "/sale-returns", new
        {
            saleId, returnDate = Today, reason = "Return one repeated catalogue line",
            lines = new[] { new { saleLineId = catalogueLine.GetProperty("saleLineId").GetGuid(), productId = p, quantity = 1m } },
            refundMethod = "Cash"
        }, K());
        Assert.Equal(HttpStatusCode.OK, catalogueReturn.StatusCode);
        var afterCatalogueReturn = await Stock(c);
        Assert.Equal(9m, afterCatalogueReturn.GetProperty("quantity").GetDecimal());
        Assert.Equal(180m, afterCatalogueReturn.GetProperty("value").GetDecimal());
    }


    [Fact] public async Task New_product_without_any_historical_cost_fails_closed_when_stock_is_zero()
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        var location = await c.Location();
        var product = await c.AddProduct("Never stocked product");
        var response = await c.Send(HttpMethod.Post, "/sales", new
        {
            customerId = (Guid?)null, locationId = location, saleDate = Today, saleType = "Cash",
            lines = new[] { new { productId = product, quantity = 1m, unitPrice = 100m, discount = 0m } }
        }, K());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty((await c.GetJson("/sales")).EnumerateArray());
    }

    [Fact] public async Task Cancelling_sale_with_open_negative_stock_restores_only_valued_inventory_and_closes_commitment()
    {
        var (c, loc, p) = await Stocked(f, qty: 5, cost: 20m);
        var response = await c.Send(HttpMethod.Post, "/sales", Sale(null, loc, p, 6, 50m, "Cash"), K());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saleId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(-1m, (await Stock(c)).GetProperty("quantity").GetDecimal());

        var cancelled = await c.Send(HttpMethod.Post, "/sales/" + saleId + "/cancel",
            new { reason = "Mistaken invoice" }, K());
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var restored = await Stock(c);
        Assert.Equal(5m, restored.GetProperty("quantity").GetDecimal());
        Assert.Equal(100m, restored.GetProperty("value").GetDecimal());

        var supplierResponse = await c.Send(HttpMethod.Post, "/suppliers", new { name = "After cancellation supplier" });
        var supplierId = (await supplierResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var receipt = await c.Send(HttpMethod.Post, "/purchases", new
        {
            supplierId, locationId = loc, receiptDate = Today,
            lines = new[] { new { productId = p, quantity = 1m, unitCost = 30m } }
        }, K());
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        var afterReceipt = await Stock(c);
        Assert.Equal(6m, afterReceipt.GetProperty("quantity").GetDecimal());
        Assert.Equal(130m, afterReceipt.GetProperty("value").GetDecimal());
    }


}
