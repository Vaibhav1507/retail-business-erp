using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

/// <summary>
/// Persists only minimal AI attempt metadata. Reservation and settlement are database-serialized
/// so Cloud Run instances share one global and per-tenant hard budget.
/// </summary>
public sealed class AiGatewayUsageLedger(TenantDb database, IConfiguration configuration, ILogger<AiGatewayUsageLedger> log)
    : IAiGatewayUsageLedger
{
    public async Task<AiBudgetReservation> ReserveAsync(
        AiGatewayRequest request, AiProviderCredential credential, string model, decimal estimatedCost, CancellationToken ct)
    {
        if (request.TenantId == Guid.Empty || request.ActorUserId == Guid.Empty)
            throw new InvalidOperationException("AI usage requires trusted tenant and user context.");

        await using var session = await database.BeginAsync(request.ActorUserId, request.TenantId, ct);
        await using var command = session.Command("""
            select reservation_id, allowed, global_month_to_date_inr, tenant_month_to_date_inr,
                   warning_threshold_reached, block_reason
            from ai_gateway.reserve_attempt(
                @tenant, @actor, @feature, @task, @provider, @model, @credential, @quota_bucket,
                @estimated_cost, @global_limit, @tenant_limit, @warning_threshold, @paid,
                @allow_paid, @correlation)
            """);
        command.Parameters.AddWithValue("tenant", request.TenantId);
        command.Parameters.AddWithValue("actor", request.ActorUserId);
        command.Parameters.AddWithValue("feature", Limit(request.Feature, 80));
        command.Parameters.AddWithValue("task", request.Task.ToString());
        command.Parameters.AddWithValue("provider", credential.Provider.ToString());
        command.Parameters.AddWithValue("model", Limit(model, 120));
        command.Parameters.AddWithValue("credential", Limit(credential.Id, 80));
        command.Parameters.AddWithValue("quota_bucket", Limit(credential.QuotaBucketId, 120));
        command.Parameters.AddWithValue("estimated_cost", estimatedCost);
        command.Parameters.AddWithValue("global_limit", configuration.GetValue("AiGateway:GlobalMonthlyBudgetInr", 1500m));
        command.Parameters.AddWithValue("tenant_limit", configuration.GetValue("AiGateway:PerTenantMonthlyBudgetInr", 50m));
        command.Parameters.AddWithValue("warning_threshold", Math.Clamp(configuration.GetValue("AiGateway:BudgetWarningThreshold", 0.75m), 0.1m, 0.99m));
        command.Parameters.AddWithValue("paid", credential.BillingMode == AiBillingMode.Paid);
        command.Parameters.AddWithValue("allow_paid", configuration.GetValue("AiGateway:AllowPaidProviders", false));
        command.Parameters.AddWithValue("correlation", Limit(request.CorrelationId ?? "", 64));

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("AI budget reservation returned no decision.");
        var id = reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0);
        var reservation = new AiBudgetReservation(id, reader.GetBoolean(1), reader.GetDecimal(2),
            reader.GetDecimal(3), reader.GetBoolean(4), reader.IsDBNull(5) ? null : reader.GetString(5));
        await reader.DisposeAsync();
        await session.CommitAsync(ct);
        return reservation;
    }

    public async Task CompleteAsync(
        AiGatewayRequest request, Guid reservationId, string outcome, int? inputTokens, int? outputTokens,
        decimal? actualCost, int latencyMilliseconds, string? errorCategory, int retryCount, CancellationToken ct)
    {
        if (reservationId == Guid.Empty) return;
        await using var session = await database.BeginAsync(request.ActorUserId, request.TenantId, ct);
        await using var command = session.Command("""
            select ai_gateway.finish_attempt(
                @tenant, @actor, @reservation, @outcome, @input_tokens, @output_tokens,
                @actual_cost, @latency_ms, @error_category, @retry_count)
            """);
        command.Parameters.AddWithValue("tenant", request.TenantId);
        command.Parameters.AddWithValue("actor", request.ActorUserId);
        command.Parameters.AddWithValue("reservation", reservationId);
        command.Parameters.AddWithValue("outcome", outcome is "Succeeded" or "Failed" ? outcome : "Failed");
        command.Parameters.AddWithValue("input_tokens", (object?)inputTokens ?? DBNull.Value);
        command.Parameters.AddWithValue("output_tokens", (object?)outputTokens ?? DBNull.Value);
        command.Parameters.AddWithValue("actual_cost", (object?)actualCost ?? DBNull.Value);
        command.Parameters.AddWithValue("latency_ms", Math.Clamp(latencyMilliseconds, 0, 600_000));
        command.Parameters.AddWithValue("error_category", errorCategory is null ? DBNull.Value : Limit(errorCategory, 80));
        command.Parameters.AddWithValue("retry_count", Math.Clamp(retryCount, 0, 10));
        var completed = await command.ExecuteScalarAsync(ct);
        if (completed is not true)
            log.LogWarning("AI usage reservation {ReservationId} was not finalized (already completed or not owned by this actor)", reservationId);
        await session.CommitAsync(ct);
    }

    private static string Limit(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim()[..Math.Min(value.Trim().Length, length)];
}
