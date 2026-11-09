using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Rbos.Api;

public sealed class DatabaseHealthCheck(NpgsqlDataSource db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var c = await db.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand("select 1", c);
            await cmd.ExecuteScalarAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unavailable", ex);
        }
    }
}
