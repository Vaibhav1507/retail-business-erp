using Npgsql;

namespace Rbos.Api.Persistence;

/// <summary>Opens a transaction on the rbos_app connection with transaction-local tenant context for RLS.</summary>
public sealed class TenantDb(NpgsqlDataSource dataSource)
{
	public async Task<TenantSession> BeginAsync(Guid userId, Guid? tenantId, CancellationToken ct)
	{
		var conn = await dataSource.OpenConnectionAsync(ct);
		var tx = await conn.BeginTransactionAsync(ct);
		var session = new TenantSession(conn, tx);
		await session.SetContextAsync(userId, tenantId, ct);
		return session;
	}

	/// <summary>Resolves the caller's single active tenant under their own user context.</summary>
	public async Task<Guid?> FindTenantAsync(Guid userId, CancellationToken ct)
	{
		await using var s = await BeginAsync(userId, null, ct);
		await using var cmd = s.Command("select tenant_id from core.tenant_membership where user_id = @u and status = 'Active' order by created_at limit 1");
		cmd.Parameters.AddWithValue("u", userId);
		var r = await cmd.ExecuteScalarAsync(ct);
		return r as Guid?;
	}
}

public sealed class TenantSession(NpgsqlConnection conn, NpgsqlTransaction tx) : IAsyncDisposable
{
	public NpgsqlCommand Command(string sql) => new(sql, conn, tx);

	public async Task SetContextAsync(Guid userId, Guid? tenantId, CancellationToken ct)
	{
		await using var cmd = Command("select set_config('app.user_id', @u, true), set_config('app.tenant_id', @t, true)");
		cmd.Parameters.AddWithValue("u", userId.ToString());
		cmd.Parameters.AddWithValue("t", tenantId?.ToString() ?? "");
		await cmd.ExecuteNonQueryAsync(ct);
	}

	public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

	public async ValueTask DisposeAsync()
	{
		await tx.DisposeAsync();
		await conn.DisposeAsync();
	}
}
