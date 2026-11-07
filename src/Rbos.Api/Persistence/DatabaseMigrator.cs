using Npgsql;

namespace Rbos.Api.Persistence;

/// <summary>
/// Applies ordered SQL migrations using the MIGRATOR role connection only.
/// The runtime application role never runs this.
/// </summary>
public static class DatabaseMigrator
{
	private const long LockKey = 7272741;

	public static async Task<IReadOnlyList<string>> ApplyAsync(
		string migratorConnectionString, string? directory = null, CancellationToken ct = default)
	{
		directory ??= Path.Combine(AppContext.BaseDirectory, "db", "migrations");
		var applied = new List<string>();

		await using var conn = new NpgsqlConnection(migratorConnectionString);
		await conn.OpenAsync(ct);
		await ExecAsync(conn, null, $"select pg_advisory_lock({LockKey})", ct);
		try
		{
			await ExecAsync(conn, null,
				"create table if not exists public.schema_migrations(name text primary key, applied_at timestamptz not null default now())", ct);
			await ExecAsync(conn, null, "alter table public.schema_migrations add column if not exists checksum text", ct);

			foreach (var file in Directory.GetFiles(directory, "*.sql").OrderBy(f => f, StringComparer.Ordinal))
			{
				var name = Path.GetFileName(file);
				var sql = await File.ReadAllTextAsync(file, ct);
				if (string.IsNullOrWhiteSpace(sql)) throw new InvalidOperationException($"Migration {name} is empty.");
				var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql.Replace("\r\n", "\n"))));
				string? recorded = null; var exists = false;
				await using (var check = new NpgsqlCommand("select checksum from public.schema_migrations where name = @n", conn))
				{
					check.Parameters.AddWithValue("n", name);
					await using var rd = await check.ExecuteReaderAsync(ct);
					if (await rd.ReadAsync(ct)) { exists = true; recorded = rd.IsDBNull(0) ? null : rd.GetString(0); }
				}
				if (exists)
				{
					if (recorded is null)
					{
						await using var fill = new NpgsqlCommand("update public.schema_migrations set checksum = @c where name = @n", conn);
						fill.Parameters.AddWithValue("c", checksum); fill.Parameters.AddWithValue("n", name);
						await fill.ExecuteNonQueryAsync(ct);
					}
					else if (recorded != checksum) throw new InvalidOperationException($"Migration {name} was changed after it was applied. Add a new migration instead.");
					continue;
				}

				await using var tx = await conn.BeginTransactionAsync(ct);
				await ExecAsync(conn, tx, sql, ct);
				await using (var ins = new NpgsqlCommand("insert into public.schema_migrations(name, checksum) values (@n, @c)", conn, tx))
				{
					ins.Parameters.AddWithValue("n", name);
					ins.Parameters.AddWithValue("c", checksum);
					await ins.ExecuteNonQueryAsync(ct);
				}
				await tx.CommitAsync(ct);
				applied.Add(name);
			}
		}
		finally
		{
			await ExecAsync(conn, null, $"select pg_advisory_unlock({LockKey})", ct);
		}
		return applied;
	}

	private static async Task ExecAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, CancellationToken ct)
	{
		await using var cmd = new NpgsqlCommand(sql, conn, tx);
		await cmd.ExecuteNonQueryAsync(ct);
	}
}
