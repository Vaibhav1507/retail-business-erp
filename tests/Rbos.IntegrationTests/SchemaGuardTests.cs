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
    [Fact]
    public async Task Legacy_item_tag_capture_queue_and_photo_store_are_removed()
    {
        var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        await using var conn = new NpgsqlConnection(cfg["ConnectionStrings:RbosTestMigrator"]);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            select
              to_regclass('integrations.item_tag_capture_queue') is null,
              to_regclass('sales.item_tag_capture') is null,
              to_regprocedure('integrations.enqueue_item_tag_capture(uuid,uuid,uuid)') is null,
              to_regprocedure('integrations.claim_item_tag_captures(integer,integer)') is null,
              to_regprocedure('integrations.finish_item_tag_capture(uuid,uuid,timestamp with time zone)') is null,
              to_regprocedure('integrations.cancel_item_tag_capture(uuid,uuid,uuid)') is null", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        for (var i = 0; i < 6; i++) Assert.True(reader.GetBoolean(i));
    }

    [Fact]
    public async Task Document_queue_operations_are_worker_only()
    {
        var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        await using var conn = new NpgsqlConnection(cfg["ConnectionStrings:RbosTestMigrator"]);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            select
              has_function_privilege('rbos_app', 'integrations.enqueue_document_processing(uuid,uuid,uuid)', 'EXECUTE'),
              has_function_privilege('rbos_app', 'integrations.claim_document_processing(integer,integer,uuid)', 'EXECUTE'),
              has_function_privilege('rbos_app', 'integrations.finish_document_processing(uuid,uuid,timestamp with time zone)', 'EXECUTE'),
              has_function_privilege('rbos_worker', 'integrations.claim_document_processing(integer,integer,uuid)', 'EXECUTE'),
              has_function_privilege('rbos_worker', 'integrations.finish_document_processing(uuid,uuid,timestamp with time zone)', 'EXECUTE'),
              has_table_privilege('rbos_worker', 'integrations.document_processing_queue', 'SELECT')", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.True(reader.GetBoolean(4));
        Assert.False(reader.GetBoolean(5));
    }

    [Fact]
    public async Task Document_extraction_storage_is_read_only_to_app_and_worker_only_for_processing()
    {
        var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        await using var conn = new NpgsqlConnection(cfg["ConnectionStrings:RbosTestMigrator"]);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            select
              has_table_privilege('rbos_app', 'documents.document_extraction_attempt', 'SELECT'),
              has_table_privilege('rbos_app', 'documents.document_extraction_draft', 'SELECT'),
              has_table_privilege('rbos_app', 'documents.document_extraction_draft', 'INSERT'),
              has_table_privilege('rbos_worker', 'documents.document_extraction_attempt', 'SELECT'),
              has_table_privilege('rbos_worker', 'documents.document_extraction_draft', 'INSERT'),
              has_function_privilege('rbos_app', 'integrations.start_document_processing(uuid,uuid,uuid)', 'EXECUTE'),
              has_function_privilege('rbos_app', 'integrations.load_document_processing_pages(uuid,uuid,uuid,uuid)', 'EXECUTE'),
              has_function_privilege('rbos_app', 'integrations.finish_document_processing(uuid,uuid,uuid,uuid,text,text,text,jsonb,jsonb,text,integer,timestamp with time zone)', 'EXECUTE'),
              has_function_privilege('rbos_worker', 'integrations.start_document_processing(uuid,uuid,uuid)', 'EXECUTE'),
              has_function_privilege('rbos_worker', 'integrations.load_document_processing_pages(uuid,uuid,uuid,uuid)', 'EXECUTE'),
              has_function_privilege('rbos_worker', 'integrations.finish_document_processing(uuid,uuid,uuid,uuid,text,text,text,jsonb,jsonb,text,integer,timestamp with time zone)', 'EXECUTE'),
              has_function_privilege('rbos_worker', 'integrations.fail_document_processing_before_start(uuid,uuid,uuid,text)', 'EXECUTE'),
              has_table_privilege('rbos_worker', 'documents.document_extraction_attempt', 'SELECT'),
              has_table_privilege('rbos_worker', 'documents.document_extraction_draft', 'SELECT')", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
        Assert.False(reader.GetBoolean(4));
        Assert.False(reader.GetBoolean(5));
        Assert.False(reader.GetBoolean(6));
        Assert.False(reader.GetBoolean(7));
        Assert.True(reader.GetBoolean(8));
        Assert.True(reader.GetBoolean(9));
        Assert.True(reader.GetBoolean(10));
        Assert.True(reader.GetBoolean(11));
        Assert.False(reader.GetBoolean(12));
        Assert.False(reader.GetBoolean(13));
    }

    [Fact]
    public async Task WhatsApp_queue_operations_are_worker_only()
    {
        var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        await using var conn = new NpgsqlConnection(cfg["ConnectionStrings:RbosTestMigrator"]);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            select
              has_function_privilege('rbos_app', 'integrations.enqueue_invoice_delivery(uuid,uuid,uuid,uuid)', 'EXECUTE'),
              has_function_privilege('rbos_app', 'integrations.claim_invoice_deliveries(integer,integer)', 'EXECUTE'),
              has_function_privilege('rbos_app', 'integrations.finish_invoice_delivery(uuid,uuid,timestamptz)', 'EXECUTE'),
              has_function_privilege('rbos_worker', 'integrations.claim_invoice_deliveries(integer,integer)', 'EXECUTE'),
              has_function_privilege('rbos_worker', 'integrations.finish_invoice_delivery(uuid,uuid,timestamptz)', 'EXECUTE'),
              has_table_privilege('rbos_worker', 'integrations.invoice_dispatch_queue', 'SELECT')", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.True(reader.GetBoolean(4));
        Assert.False(reader.GetBoolean(5));
    }

}
