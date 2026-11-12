using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Rbos.IntegrationTests;

public class SchemaGuardTests
{
    [Fact]
    public async Task Every_table_with_tenant_id_has_forced_row_level_security()
    {
        var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        await using var conn = new NpgsqlConnection(cfg["ConnectionStrings:RbosTestMigrator"]);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            select n.nspname || '.' || c.relname
            from pg_class c
            join pg_namespace n on n.oid = c.relnamespace
            join pg_attribute a on a.attrelid = c.oid and a.attname = 'tenant_id' and not a.attisdropped
            where c.relkind = 'r' and not (c.relrowsecurity and c.relforcerowsecurity)
              and n.nspname not in ('pg_catalog','information_schema')", conn);
        var missing = new List<string>();
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync()) missing.Add(rd.GetString(0));
        Assert.Empty(missing);
    }

    [Fact]
    public async Task Application_role_cannot_bypass_rls_or_own_tables()
    {
        var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        await using var conn = new NpgsqlConnection(cfg["ConnectionStrings:RbosTestMigrator"]);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            select (select rolbypassrls or rolsuper from pg_roles where rolname = 'rbos_app'),
                   (select count(*) from pg_tables where tableowner = 'rbos_app')", conn);
        await using var rd = await cmd.ExecuteReaderAsync();
        await rd.ReadAsync();
        Assert.False(rd.GetBoolean(0));
        Assert.Equal(0L, rd.GetInt64(1));
    }
}
