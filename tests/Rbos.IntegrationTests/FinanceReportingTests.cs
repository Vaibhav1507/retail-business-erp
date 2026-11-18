using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Rbos.Api.Persistence;

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
    public async Task Stock_verification_resolves_migration_044_aliases_and_detects_corrupt_projections()
    {
        var c = new Client(f); await c.SignUpAndBootstrap();
        var location = await c.Location();
        var canonical = await c.AddProduct("Alias canonical cable");
        var retired = await c.AddProduct("Alias duplicate cable");
        var supplier = await Id(await c.Send(HttpMethod.Post, "/suppliers", new { name = "Alias test supplier" }));
        await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = supplier, locationId = location, receiptDate = Today, lines = new[] { new { productId = canonical, quantity = 5m, unitCost = 100m } } }, K()));
        var retiredReceipt = await Id(await c.Send(HttpMethod.Post, "/purchases", new { supplierId = supplier, locationId = location, receiptDate = Today, lines = new[] { new { productId = retired, quantity = 10m, unitCost = 200m } } }, K()));
        var cancelSale = await Id(await c.Send(HttpMethod.Post, "/sales", new { locationId = location, saleDate = Today, saleType = "Cash", amountPaid = 300m, payMethod = "Cash", lines = new[] { new { productId = retired, quantity = 1m, unitPrice = 300m, discount = 0m } } }, K()));
        var returnSale = await Id(await c.Send(HttpMethod.Post, "/sales", new { locationId = location, saleDate = Today, saleType = "Cash", amountPaid = 300m, payMethod = "Cash", lines = new[] { new { productId = retired, quantity = 1m, unitPrice = 300m, discount = 0m } } }, K()));

        using var scope = f.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var uid = (await users.FindByNameAsync(c.Email))!.Id;
        var db = scope.ServiceProvider.GetRequiredService<TenantDb>();
        var tenant = (await db.FindTenantAsync(uid, default))!.Value;

        // This is deliberate fixture-level projection corruption/merge setup. Use the
        // dedicated migrator connection, not rbos_app: runtime grants must not permit
        // arbitrary stock_balance deletes. FORCE ROW LEVEL SECURITY still applies.
        var config = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        var migratorConnectionString = config["ConnectionStrings:RbosTestMigrator"];
        Assert.False(string.IsNullOrWhiteSpace(migratorConnectionString));
        await using var connection = new NpgsqlConnection(migratorConnectionString);
        await connection.OpenAsync();
        await using (var tx = await connection.BeginTransactionAsync())
        {
            await using (var context = new NpgsqlCommand(
                "select set_config('app.user_id', @u, true), set_config('app.tenant_id', @t, true)", connection, tx))
            {
                context.Parameters.AddWithValue("u", uid.ToString());
                context.Parameters.AddWithValue("t", tenant.ToString());
                await context.ExecuteNonQueryAsync();
            }

            async Task Execute(string sql, Action<NpgsqlCommand>? configure = null)
            {
                await using var command = new NpgsqlCommand(sql, connection, tx);
                configure?.Invoke(command);
                await command.ExecuteNonQueryAsync();
            }

            // FORCE RLS also applies to the table owner. Add a tenant-scoped,
            // transaction-only policy for the migrator fixture role, then remove it.
            // This keeps runtime rbos_app grants and production policies unchanged.
            foreach (var table in new[] { "catalog.product_alias", "inventory.stock_balance", "inventory.item_cost", "catalog.product" })
                await Execute($"create policy rbos_test_fixture_tenant_access on {table} for all to rbos_migrator using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)");

            await Execute(
                "insert into catalog.product_alias(tenant_id,alias_product_id,canonical_product_id) values (@t,@a,@c)",
                command => { command.Parameters.AddWithValue("t", tenant); command.Parameters.AddWithValue("a", retired); command.Parameters.AddWithValue("c", canonical); });
            await Execute(@"insert into inventory.stock_balance(tenant_id,product_id,location_id,quantity)
                select tenant_id,@c,location_id,quantity from inventory.stock_balance where product_id=@a
                on conflict (tenant_id,product_id,location_id) do update set quantity=inventory.stock_balance.quantity+excluded.quantity",
                command => { command.Parameters.AddWithValue("c", canonical); command.Parameters.AddWithValue("a", retired); });
            await Execute("delete from inventory.stock_balance where product_id=@a",
                command => command.Parameters.AddWithValue("a", retired));
            await Execute(@"insert into inventory.item_cost(tenant_id,product_id,total_quantity,inventory_value,wac)
                select tenant_id,@c,total_quantity,inventory_value,wac from inventory.item_cost where product_id=@a
                on conflict (tenant_id,product_id) do update set total_quantity=inventory.item_cost.total_quantity+excluded.total_quantity,
                  inventory_value=inventory.item_cost.inventory_value+excluded.inventory_value,
                  wac=round((inventory.item_cost.inventory_value+excluded.inventory_value)/nullif(inventory.item_cost.total_quantity+excluded.total_quantity,0),4)",
                command => { command.Parameters.AddWithValue("c", canonical); command.Parameters.AddWithValue("a", retired); });
            await Execute("delete from inventory.item_cost where product_id=@a",
                command => command.Parameters.AddWithValue("a", retired));
            await Execute("update catalog.product set is_active=false where id=@a",
                command => command.Parameters.AddWithValue("a", retired));
            foreach (var table in new[] { "catalog.product_alias", "inventory.stock_balance", "inventory.item_cost", "catalog.product" })
                await Execute($"drop policy rbos_test_fixture_tenant_access on {table}");
            await tx.CommitAsync();
        }

        // Historical sale and receipt documents still refer to the retired product ID.
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, $"/sales/{cancelSale}/cancel", new { reason = "Alias replay regression" }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/sale-returns", new { saleId = returnSale, returnDate = Today, reason = "Alias return regression", lines = new[] { new { productId = retired, quantity = 1m } } }, K())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, "/purchase-returns", new { receiptId = retiredReceipt, returnDate = Today, reason = "Alias supplier return regression", lines = new[] { new { productId = retired, quantity = 1m } } }, K())).StatusCode);

        var healthy = await c.GetJson("/reports/stock-verification");
        Assert.True(healthy.GetProperty("ok").GetBoolean());
        Assert.DoesNotContain(healthy.GetProperty("items").EnumerateArray(), x => x.GetProperty("productId").GetGuid() == retired);

        // Corrupt the projection through the same isolated test-fixture connection.
        await using (var tx = await connection.BeginTransactionAsync())
        {
            await using (var context = new NpgsqlCommand(
                "select set_config('app.user_id', @u, true), set_config('app.tenant_id', @t, true)", connection, tx))
            {
                context.Parameters.AddWithValue("u", uid.ToString());
                context.Parameters.AddWithValue("t", tenant.ToString());
                await context.ExecuteNonQueryAsync();
            }
            await using (var policy = new NpgsqlCommand(
                "create policy rbos_test_fixture_tenant_access on inventory.stock_balance for all to rbos_migrator using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)", connection, tx))
                await policy.ExecuteNonQueryAsync();
            await using (var corrupt = new NpgsqlCommand(
                "update inventory.stock_balance set quantity=quantity+1 where product_id=@p", connection, tx))
            {
                corrupt.Parameters.AddWithValue("p", canonical);
                Assert.Equal(1, await corrupt.ExecuteNonQueryAsync());
            }
            await using (var dropPolicy = new NpgsqlCommand(
                "drop policy rbos_test_fixture_tenant_access on inventory.stock_balance", connection, tx))
                await dropPolicy.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }

        var damaged = await c.GetJson("/reports/stock-verification");
        Assert.False(damaged.GetProperty("ok").GetBoolean());
        Assert.Contains(damaged.GetProperty("items").EnumerateArray(), x => x.GetProperty("productId").GetGuid() == canonical);
        var other = new Client(f); await other.SignUpAndBootstrap();
        var isolated = await other.GetJson("/reports/stock-verification");
        Assert.True(isolated.GetProperty("ok").GetBoolean());
        Assert.DoesNotContain(isolated.GetProperty("items").EnumerateArray(), x => x.GetProperty("name").GetString() == "Alias canonical cable");
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
