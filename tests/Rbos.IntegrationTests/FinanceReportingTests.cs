using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class FinanceReportingTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    static string K() => Guid.NewGuid().ToString();
    static async Task<JsonElement> Body(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();
    static async Task<Guid> Id(HttpResponseMessage r) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); return (await Body(r)).GetProperty("id").GetGuid(); }

    [Fact]
    public async Task Loan_is_recorded_repaid_in_parts_and_cannot_be_over_repaid()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var m = (await c.GetJson("/money-accounts"))[0].GetProperty("id").GetGuid();

        var loanId = await Id(await c.Send(HttpMethod.Post, "/loans", new { lender = "City Bank", date = Today, moneyAccountId = m, principal = 10000m, interestRate = 12m }, K()));

        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, $"/loans/{loanId}/repayments", new { date = Today, moneyAccountId = m, principal = 2000m, interest = 100m }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, $"/loans/{loanId}/repayments", new { date = Today, moneyAccountId = m, principal = 9000m, interest = 0m }, K())).StatusCode);

        var loan = (await c.GetJson("/loans")).EnumerateArray().First(x => x.GetProperty("id").GetGuid() == loanId);
        Assert.Equal(2000m, loan.GetProperty("principalPaid").GetDecimal());
        Assert.Equal(100m, loan.GetProperty("interestPaid").GetDecimal());
        Assert.Equal(8000m, loan.GetProperty("outstanding").GetDecimal());

        var tb = await c.GetJson("/reports/trial-balance?asOf=" + Today);
        Assert.Equal(tb.GetProperty("lines").EnumerateArray().Sum(l => l.GetProperty("debit").GetDecimal()),
                     tb.GetProperty("lines").EnumerateArray().Sum(l => l.GetProperty("credit").GetDecimal()));
    }

    [Fact]
    public async Task Loan_request_is_idempotent_and_validated()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var m = (await c.GetJson("/money-accounts"))[0].GetProperty("id").GetGuid();
        var key = K();
        var body = new { lender = "Local Lender", date = Today, moneyAccountId = m, principal = 500m, interestRate = 0m };
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/loans", body, key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/loans", body, key)).StatusCode);
        Assert.Equal(1, (await c.GetJson("/loans")).GetArrayLength());

        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/loans", new { lender = "X", date = Today, moneyAccountId = m, principal = 500m, interestRate = 150m }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/loans", new { lender = "X", date = Today, moneyAccountId = m, principal = 0m, interestRate = 5m }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/loans", body)).StatusCode);
        Assert.Equal(1, (await c.GetJson("/loans")).GetArrayLength());
    }

    [Fact]
    public async Task Custom_accounts_can_be_added_and_renamed_but_system_accounts_cannot()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var created = await c.Send(HttpMethod.Post, "/accounts", new { name = "Packaging", type = "Expense" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await Body(created)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, $"/accounts/{id}", new { name = "Packaging & freight", type = "Expense" })).StatusCode);
        var chart = await c.GetJson("/accounts");
        Assert.Contains(chart.EnumerateArray(), a => a.GetProperty("name").GetString() == "Packaging & freight");

        var system = chart.EnumerateArray().First(a => a.GetProperty("id").GetGuid() != id);
        var systemId = system.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Put, $"/accounts/{systemId}", new { name = "Hacked", type = "Asset" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Send(HttpMethod.Put, $"/accounts/{Guid.NewGuid()}", new { name = "Ghost", type = "Asset" })).StatusCode);
    }

    [Fact]
    public async Task Custom_account_can_be_deactivated_but_system_account_and_unknown_cannot()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var id = (await Body(await c.Send(HttpMethod.Post, "/accounts", new { name = "Retired", type = "Expense" }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, $"/accounts/{id}/active", new { active = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, $"/accounts/{id}/active", new { active = true })).StatusCode);
        var systemId = (await c.GetJson("/accounts")).EnumerateArray().First(a => a.GetProperty("id").GetGuid() != id).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Put, $"/accounts/{systemId}/active", new { active = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Send(HttpMethod.Put, $"/accounts/{Guid.NewGuid()}/active", new { active = false })).StatusCode);
    }

    [Fact]
    public async Task Loan_schedule_lists_payments_and_unknown_loan_is_404()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var m = (await c.GetJson("/money-accounts"))[0].GetProperty("id").GetGuid();
        var loanId = await Id(await c.Send(HttpMethod.Post, "/loans", new { lender = "City Bank", date = Today, moneyAccountId = m, principal = 10000m, interestRate = 12m }, K()));
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, $"/loans/{loanId}/repayments", new { date = Today, moneyAccountId = m, principal = 2000m, interest = 50m }, K())).StatusCode);
        var res = await c.Send(HttpMethod.Get, $"/loans/{loanId}/schedule");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(1, (await Body(res)).GetProperty("payments").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await c.Send(HttpMethod.Get, $"/loans/{Guid.NewGuid()}/schedule")).StatusCode);
    }

    [Fact]
    public async Task Export_is_recorded_in_the_audit_trail_and_validated()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/exports/audit", new { report = "Trial balance", rows = 12 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/exports/audit", new { report = "", rows = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/exports/audit", new { report = "X", rows = -1 })).StatusCode);
    }

    [Fact]
    public async Task Stock_verification_reports_ok_with_replay_list_for_a_fresh_tenant()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var body = await c.GetJson("/reports/stock-verification");
        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal(0, body.GetProperty("replay").GetArrayLength());
    }

    [Fact]
    public async Task Account_creation_validates_name_and_type_and_hands_out_distinct_codes()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/accounts", new { name = "", type = "Expense" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/accounts", new { name = "Odd", type = "Equity" })).StatusCode);
        var a = (await Body(await c.Send(HttpMethod.Post, "/accounts", new { name = "Misc one", type = "Income" }))).GetProperty("code").GetString();
        var b = (await Body(await c.Send(HttpMethod.Post, "/accounts", new { name = "Misc two", type = "Income" }))).GetProperty("code").GetString();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task Stock_verification_is_clean_after_normal_activity()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location(); var p = await c.AddProduct("Fan");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 6m, unitCost = 100m } } }, K()));
        await Id(await c.Send(HttpMethod.Post, "/sales", new { locationId = loc, saleDate = Today, saleType = "Cash", amountPaid = 300m, payMethod = "Cash", lines = new[] { new { productId = p, quantity = 2m, unitPrice = 150m, discount = 0m } } }, K()));

        var v = await c.GetJson("/reports/stock-verification");
        Assert.True(v.GetProperty("ok").GetBoolean());
        Assert.Equal(0, v.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/reports/stock-verification")).StatusCode);
    }

    [Fact]
    public async Task Gst_summary_splits_tax_by_rate_and_validates_range()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var loc = await c.Location(); var p = await c.AddProduct("Mixer");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 10m, unitCost = 50m } } }, K()));
        await Id(await c.Send(HttpMethod.Post, "/sales", new { locationId = loc, saleDate = Today, saleType = "Cash", amountPaid = 118m, payMethod = "Cash", gstRate = 18m, lines = new[] { new { productId = p, quantity = 1m, unitPrice = 118m, discount = 0m } } }, K()));

        var r = await c.Send(HttpMethod.Get, $"/reports/gst-summary?from={Today}&to={Today}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var j = await Body(r);
        Assert.Equal(1, j.GetProperty("lines").GetArrayLength());
        var line = j.GetProperty("lines")[0];
        Assert.Equal(18m, line.GetProperty("rate").GetDecimal());
        Assert.Equal(100m, line.GetProperty("taxable").GetDecimal());
        Assert.Equal(18m, line.GetProperty("tax").GetDecimal());
        Assert.Equal(18m, j.GetProperty("totals").GetProperty("tax").GetDecimal());

        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Get, "/reports/gst-summary?from=2026-12-31&to=2026-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync($"/api/reports/gst-summary?from={Today}&to={Today}")).StatusCode);
    }

    [Fact]
    public async Task Reminders_list_receivables_payables_and_low_stock_and_require_login()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var empty = await c.GetJson("/reminders");
        Assert.Equal(0, empty.GetArrayLength());

        var loc = await c.Location(); var p = await c.AddProduct("Bulb");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Acme" }));
        var cust = await Id(await c.Send(HttpMethod.Post, "/customers", new { name = "Ravi", phone = "9990003333" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = sup, locationId = loc, receiptDate = Today, lines = new[] { new { productId = p, quantity = 10m, unitCost = 10m } } }, K()));
        await Id(await c.Send(HttpMethod.Post, "/sales", new { customerId = cust, locationId = loc, saleDate = Today, saleType = "Credit", amountPaid = 0m, payMethod = "Cash", lines = new[] { new { productId = p, quantity = 2m, unitPrice = 30m, discount = 0m } } }, K()));

        var list = await c.GetJson("/reminders");
        var kinds = list.EnumerateArray().Select(x => x.GetProperty("kind").GetString()).ToList();
        Assert.Contains("collect", kinds);
        Assert.Contains("pay", kinds);
        Assert.Equal(60m, list.EnumerateArray().First(x => x.GetProperty("kind").GetString() == "collect").GetProperty("amount").GetDecimal());
        Assert.Equal(100m, list.EnumerateArray().First(x => x.GetProperty("kind").GetString() == "pay").GetProperty("amount").GetDecimal());

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/reminders")).StatusCode);
    }

    [Fact]
    public async Task Reminders_flag_overdue_purchase_orders_and_stay_isolated_per_business()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var other = new Client(f); await other.SignUpAndBootstrap();
        var p = await c.AddProduct("Switch");
        var sup = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Globex" }));
        var past = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd");
        await Id(await c.Send(HttpMethod.Post, "/purchase-orders", new { supplierId = sup, date = past, expectedDate = past, lines = new[] { new { productId = p, quantity = 3m, unitCost = 9m } } }));

        var mine = await c.GetJson("/reminders");
        var orders = mine.EnumerateArray().First(x => x.GetProperty("kind").GetString() == "orders");
        Assert.Equal(1, orders.GetProperty("count").GetInt32());

        var theirs = await other.GetJson("/reminders");
        Assert.DoesNotContain(theirs.EnumerateArray(), x => x.GetProperty("kind").GetString() == "orders");
    }
}
