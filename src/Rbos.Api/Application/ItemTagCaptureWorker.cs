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
        var maxJobsPerRun = Math.Clamp(config.GetValue("AiPerformance:ItemTagQueueMaxJobsPerRun", 5), 1, 5);
        var processed = 0;
        logger.LogInformation("Item-tag worker started. Maximum jobs this run {MaxJobs}.", maxJobsPerRun);

        while (!ct.IsCancellationRequested && processed < maxJobsPerRun)
        {
            var claimed = await ClaimAsync(queueDataSource, ct);
            if (claimed is null) break;
            processed++;

            // A lease that had to be reclaimed means the previous process may have
            // completed a paid provider call but crashed before persisting the result.
            // Do not repeat the AI request blindly; preserve the photo for manual review.
            if (claimed.AttemptCount > 1)
            {
                await service.MarkNeedsAttentionAsync(claimed.ActorUserId, claimed.TenantId, claimed.CaptureId, ct);
                await FinishAsync(queueDataSource, claimed, null, ct);
                logger.LogWarning("Item-tag job lease was recovered after an uncertain attempt. Capture {CaptureId} moved to NeedsAttention without replaying provider calls.", claimed.CaptureId);
                continue;
            }

            try
            {
                await service.ProcessAsync(claimed.ActorUserId, claimed.TenantId, claimed.CaptureId, ct);
                await FinishAsync(queueDataSource, claimed, null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Keep the lease. A later scheduled execution will recover it, without
                // repeating a possibly-paid provider attempt.
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Item-tag job failed. Capture {CaptureId}, attempt {AttemptCount}, error type {ErrorType}.",
                    claimed.CaptureId, claimed.AttemptCount, ex.GetType().Name);
                await service.MarkNeedsAttentionAsync(claimed.ActorUserId, claimed.TenantId, claimed.CaptureId, ct);
                await FinishAsync(queueDataSource, claimed, null, ct);
            }
        }

        logger.LogInformation("Item-tag worker finished. Jobs handled {ProcessedJobs}, run limit {MaxJobs}.", processed, maxJobsPerRun);
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
