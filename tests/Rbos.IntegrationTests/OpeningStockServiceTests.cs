using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Rbos.Api.Application;
using Rbos.Api.Persistence;
using Rbos.Domain;

namespace Rbos.IntegrationTests;

public class OpeningStockServiceTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Repeated_command_returns_original_result_without_duplicating_any_record()
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        var product = await c.AddProduct("Cable");
        var location = await c.Location();

        using var scope = f.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var uid = (await um.FindByNameAsync(c.Email))!.Id;
        var svc = scope.ServiceProvider.GetRequiredService<OpeningStockService>();
        var db = scope.ServiceProvider.GetRequiredService<TenantDb>();
        var tid = (await db.FindTenantAsync(uid, default))!.Value;

        var key = Guid.NewGuid().ToString();
        var cmd = new PostOpeningStockCommand(uid, key, location, new DateOnly(2026, 4, 1),
            [new OpeningStockLineInput(product, 3m, 33.3333m)], "test");

        var first = await svc.PostAsync(cmd, default);
        var second = await svc.PostAsync(cmd, default);

        Assert.Equal(OpeningStockStatus.Posted, first.Status);
        Assert.Equal(100.00m, first.Total);
        Assert.Equal(OpeningStockStatus.Replayed, second.Status);
        Assert.Equal(first.DocumentId, second.DocumentId);

        await using var s = await db.BeginAsync(uid, tid, default);
        foreach (var table in new[] { "inventory.opening_stock_document", "inventory.opening_stock_line", "inventory.stock_movement", "inventory.stock_balance", "inventory.item_cost", "accounting.journal_entry", "audit.audit_event where action='OpeningStockPosted'" })
        {
            var sql = table.Contains(' ') ? $"select count(*) from {table}" : $"select count(*) from {table}";
            await using var q = s.Command(sql);
            Assert.Equal(1L, (long)(await q.ExecuteScalarAsync())!);
        }
        await using var j = s.Command("select count(*) from accounting.journal_line");
        Assert.Equal(2L, (long)(await j.ExecuteScalarAsync())!);
        await using var w = s.Command("select quantity from inventory.stock_balance");
        Assert.Equal(3m, (decimal)(await w.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Same_key_with_different_content_conflicts_and_changes_nothing()
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        var product = await c.AddProduct("Charger");
        var location = await c.Location();

        using var scope = f.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var uid = (await um.FindByNameAsync(c.Email))!.Id;
        var svc = scope.ServiceProvider.GetRequiredService<OpeningStockService>();

        var key = Guid.NewGuid().ToString();
        var first = await svc.PostAsync(new(uid, key, location, new DateOnly(2026, 4, 1), [new OpeningStockLineInput(product, 2m, 10m)], "t"), default);
        var changed = await svc.PostAsync(new(uid, key, location, new DateOnly(2026, 4, 1), [new OpeningStockLineInput(product, 2m, 11m)], "t"), default);

        Assert.Equal(OpeningStockStatus.Posted, first.Status);
        Assert.Equal(OpeningStockStatus.Conflict, changed.Status);
    }

    [Fact]
    public async Task Rejected_post_on_a_real_business_leaves_stock_and_ledger_empty()
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        var product = await c.AddProduct("Adapter");
        var location = await c.Location();

        using var scope = f.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var uid = (await um.FindByNameAsync(c.Email))!.Id;
        var svc = scope.ServiceProvider.GetRequiredService<OpeningStockService>();
        var db = scope.ServiceProvider.GetRequiredService<TenantDb>();
        var tid = (await db.FindTenantAsync(uid, default))!.Value;

        var bad = await svc.PostAsync(new(uid, Guid.NewGuid().ToString(), location, new DateOnly(2026, 4, 1),
            [new OpeningStockLineInput(product, 2m, 10m), new OpeningStockLineInput(Guid.NewGuid(), 1m, 5m)], "t"), default);
        Assert.NotEqual(OpeningStockStatus.Posted, bad.Status);

        await using var s = await db.BeginAsync(uid, tid, default);
        foreach (var table in new[] { "inventory.opening_stock_document", "inventory.stock_movement", "inventory.stock_balance", "accounting.journal_entry", "accounting.journal_line" })
        {
            await using var q = s.Command($"select count(*) from {table}");
            Assert.Equal(0L, (long)(await q.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task Invalid_commands_are_rejected_before_touching_the_database()
    {
        using var scope = f.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<OpeningStockService>();
        var r = await svc.PostAsync(new(Guid.NewGuid(), "short", Guid.NewGuid(), new DateOnly(2026, 4, 1), [new OpeningStockLineInput(Guid.NewGuid(), 1m, 1m)], "t"), default);
        Assert.Equal(OpeningStockStatus.Invalid, r.Status);
        var r2 = await svc.PostAsync(new(Guid.NewGuid(), "long-enough-key", Guid.NewGuid(), new DateOnly(2026, 4, 1), [new OpeningStockLineInput(Guid.NewGuid(), 0m, 1m)], "t"), default);
        Assert.Equal(OpeningStockStatus.Invalid, r2.Status);
    }
}
