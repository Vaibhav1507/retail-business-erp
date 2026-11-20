using Npgsql;
using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

/// <summary>Durable worker for private item-tag capture jobs.</summary>
public static class ItemTagCaptureWorker
{
    sealed record Claimed(Guid CaptureId, Guid TenantId, Guid ActorUserId, Guid LeaseToken, int AttemptCount);

    public static async Task RunAsync(
        NpgsqlDataSource dataSource,
        NpgsqlDataSource queueDataSource,
        IConfiguration config,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("ItemTagCaptureWorker");
        var db = new TenantDb(dataSource);
        using var geminiHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        using var groqHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var groq = new GroqVisionClient(groqHttp, config, loggerFactory.CreateLogger<GroqVisionClient>());
        var service = new ItemTagCaptureService(
            db, geminiHttp, groq, config, loggerFactory.CreateLogger<ItemTagCaptureService>());
        var pollSeconds = Math.Clamp(config.GetValue("AiPerformance:ItemTagQueuePollSeconds", 2), 1, 15);

        logger.LogInformation("Item-tag worker started. Poll interval {PollSeconds}s.", pollSeconds);
        while (!ct.IsCancellationRequested)
        {
            var claimed = await ClaimAsync(queueDataSource, ct);
            if (claimed is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(pollSeconds), ct);
                continue;
            }

            try
            {
                await service.ProcessAsync(claimed.ActorUserId, claimed.TenantId, claimed.CaptureId, ct);
                await FinishAsync(queueDataSource, claimed, null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Keep the lease. Another process can safely reclaim it after expiry.
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Item-tag job failed. Capture {CaptureId}, attempt {AttemptCount}, error type {ErrorType}.",
                    claimed.CaptureId, claimed.AttemptCount, ex.GetType().Name);
                DateTimeOffset? retryAt = claimed.AttemptCount < 5
                    ? DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 10 * Math.Pow(2, claimed.AttemptCount - 1)))
                    : null;
                if (retryAt is null)
                    await service.MarkNeedsAttentionAsync(claimed.ActorUserId, claimed.TenantId, claimed.CaptureId, ct);
                await FinishAsync(queueDataSource, claimed, retryAt, ct);
            }
        }
    }

    static async Task<Claimed?> ClaimAsync(NpgsqlDataSource source, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            "select capture_id,tenant_id,actor_user_id,lease_token,attempt_count from integrations.claim_item_tag_captures(1,300)",
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Claimed(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetInt32(4));
    }

    static async Task FinishAsync(
        NpgsqlDataSource source, Claimed item, DateTimeOffset? retryAt, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            "select integrations.finish_item_tag_capture(@id,@lease,@retry)", connection);
        command.Parameters.AddWithValue("id", item.CaptureId);
        command.Parameters.AddWithValue("lease", item.LeaseToken);
        command.Parameters.AddWithValue("retry", (object?)retryAt ?? DBNull.Value);
        await command.ExecuteScalarAsync(ct);
    }
}
