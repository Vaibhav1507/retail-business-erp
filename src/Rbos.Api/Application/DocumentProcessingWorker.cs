using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Rbos.Api.Application;

namespace Rbos.Api.Application;

public static class DocumentProcessingWorker
{
    const int MaxJobsPerRun = 2;
    const int MaxAttempts = 4;
    const string SalesSchemaVersion = "sale-v1";
    const string PurchaseSchemaVersion = "purchase-v1";
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    sealed record Claimed(Guid DocumentId, Guid TenantId, Guid ActorUserId, Guid LeaseToken, int AttemptCount);
    sealed record Started(Guid AttemptId, int AttemptNumber, string Direction, string SourceKind);
    sealed record SourcePage(int PageNumber, string FileName, string ContentType, int SizeBytes, string Sha256, byte[] Bytes);

    public static async Task RunAsync(
        NpgsqlDataSource queueDataSource,
        IServiceProvider services,
        ILogger logger,
        CancellationToken ct,
        Guid? onlyDocumentId = null)
    {
        var claimed = await ClaimAsync(queueDataSource, ct, onlyDocumentId);
        if (claimed.Count == 0) return;

        logger.LogInformation("Document processing worker claimed {Count} documents", claimed.Count);
        foreach (var item in claimed)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessOneAsync(queueDataSource, services, item, logger, ct);
        }
    }

    static async Task<List<Claimed>> ClaimAsync(NpgsqlDataSource source, CancellationToken ct, Guid? onlyDocumentId)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            "select document_id, tenant_id, actor_user_id, lease_token, attempt_count from integrations.claim_document_processing(@limit, @lease, @document)",
            conn);
        command.Parameters.AddWithValue("limit", onlyDocumentId is null ? MaxJobsPerRun : 1);
        command.Parameters.AddWithValue("lease", 900);
        command.Parameters.AddWithValue("document", (object?)onlyDocumentId ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<Claimed>();
        while (await reader.ReadAsync(ct))
            result.Add(new Claimed(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetInt32(4)));
        return result;
    }

    static async Task ProcessOneAsync(
        NpgsqlDataSource queueDataSource,
        IServiceProvider services,
        Claimed item,
        ILogger logger,
        CancellationToken ct)
    {
        Started? started = null;
        var pages = new List<SourcePage>();
        var timer = Stopwatch.StartNew();
        try
        {
            started = await StartAsync(queueDataSource, item, ct);
            if (started is null)
            {
                // The state or lease changed after claiming. Retire only our own lease.
                await FinishLegacyQueueAsync(queueDataSource, item, null, ct);
                return;
            }

            pages = await LoadPagesAsync(queueDataSource, item, started.AttemptId, ct);
            if (pages.Count is < 1 or > 6)
                throw new InvalidDataException("Document page count is outside the supported range.");

            var actualBytes = pages.Sum(page => (long)page.Bytes.Length);
            if (actualBytes is <= 0 or > 24L * 1024 * 1024)
                throw new InvalidDataException("Document source bytes are outside the supported size limit.");

            foreach (var page in pages)
            {
                if (page.Bytes.Length != page.SizeBytes)
                    throw new InvalidDataException("A stored document page length does not match its metadata.");
                var hash = Convert.ToHexString(SHA256.HashData(page.Bytes)).ToLowerInvariant();
                if (!CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(hash),
                    System.Text.Encoding.ASCII.GetBytes(page.Sha256.Trim())))
                    throw new InvalidDataException("A stored document page failed its checksum.");
            }

            using var scope = services.CreateScope();
            ServiceResult<object> result;
            string extractor;
            string schemaVersion;

            if (started.Direction == "Sale")
            {
                extractor = "SalesInvoiceCapture";
                schemaVersion = SalesSchemaVersion;
                var capture = scope.ServiceProvider.GetRequiredService<SalesInvoiceCaptureService>();
                result = await capture.ExtractAsync(
                    pages.Select(page => ((string?)page.ContentType, page.Bytes)).ToArray(), ct);
            }
            else if (started.Direction == "Purchase")
            {
                extractor = "SupplierInvoiceCapture";
                schemaVersion = PurchaseSchemaVersion;
                var extraction = scope.ServiceProvider.GetRequiredService<InvoiceExtractionService>();
                result = await extraction.ExtractForDocumentAsync(
                    item.ActorUserId,
                    pages.Select(page => ((string?)page.ContentType, page.Bytes)).ToArray(),
                    ct);
            }
            else
            {
                throw new InvalidDataException("Document direction is not supported.");
            }

            var data = result.Error is null
                ? JsonSerializer.SerializeToNode(result.Value, JsonOptions) as JsonObject
                : null;
            if (result.Error is not null)
            {
                // A user-actionable extraction result (for example, no active uploader
                // context or no readable supplier rows) is not retried as a queue outage.
                var safeCode = result.Status == 409 ? "uploader_context_unavailable" : "extraction_needs_attention";
                var userWarnings = new JsonArray(JsonValue.Create(result.Error));
                await FinishAsync(queueDataSource, item, started, "FailedNeedsAction",
                    null, null, null, userWarnings, safeCode, (int)timer.ElapsedMilliseconds, null, ct);
                logger.LogWarning(
                    "Document extraction requires user action. Document {DocumentId}, direction {Direction}, attempt {Attempt}, code {Code}",
                    item.DocumentId, started.Direction, started.AttemptNumber, safeCode);
                return;
            }

            if (data is null)
                throw new InvalidOperationException("Extraction provider result could not be serialized.");

            data["direction"] = started.Direction;
            data["sourceKind"] = started.SourceKind;
            data["extractor"] = extractor;
            data["schemaVersion"] = schemaVersion;

            var warnings = data["warnings"] is JsonArray warningArray
                ? (JsonArray)warningArray.DeepClone()
                : new JsonArray();
            var provider = data["provider"]?.GetValue<string>();
            var model = data["model"]?.GetValue<string>();

            await FinishAsync(queueDataSource, item, started, "NeedsReview",
                provider, model, data, warnings, null, (int)timer.ElapsedMilliseconds, null, ct);

            logger.LogInformation(
                "Document extraction completed for review. Document {DocumentId}, direction {Direction}, pages {PageCount}, provider {Provider}, model {Model}, attempt {Attempt}, duration {DurationMs}ms",
                item.DocumentId, started.Direction, pages.Count, provider, model, started.AttemptNumber, timer.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // A scheduled job is stopping. Keep the live lease; it expires and can be
            // reclaimed by the next run. Do not mark a partial extraction as complete.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Document processing failed. Document {DocumentId}, tenant {TenantId}, attempt {Attempt}",
                item.DocumentId, item.TenantId, started?.AttemptNumber ?? item.AttemptCount);

            if (started is null)
            {
                var canRetryBeforeStart = IsRetryable(ex) && item.AttemptCount < MaxAttempts;
                var retryBeforeStart = canRetryBeforeStart
                    ? DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Max(0, item.AttemptCount - 1))))
                    : (DateTimeOffset?)null;
                try
                {
                    if (canRetryBeforeStart)
                        await FinishLegacyQueueAsync(queueDataSource, item, retryBeforeStart, ct);
                    else
                        await FailBeforeStartAsync(queueDataSource, item, SafeErrorCode(ex), ct);
                }
                catch (Exception finishError)
                {
                    // If the database is unavailable the lease will expire and be reclaimed;
                    // never silently delete an unprocessed document from the queue.
                    logger.LogError(finishError, "Could not complete pre-extraction failure for document {DocumentId}", item.DocumentId);
                }
                return;
            }

            var attempt = started.AttemptNumber;
            var retryable = IsRetryable(ex) && attempt < MaxAttempts;
            var state = retryable ? "FailedRetryable" : "FailedNeedsAction";
            var retryAt = retryable
                ? DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Max(0, attempt - 1))))
                : (DateTimeOffset?)null;
            var code = SafeErrorCode(ex);
            try
            {
                await FinishAsync(queueDataSource, item, started, state,
                    null, null, null, new JsonArray(), code, (int)timer.ElapsedMilliseconds, retryAt, ct);
            }
            catch (Exception finishError)
            {
                // The lease will expire automatically. Do not log source document contents.
                logger.LogError(finishError, "Could not persist document processing failure {DocumentId}", item.DocumentId);
            }
        }
        finally
        {
            foreach (var page in pages)
                CryptographicOperations.ZeroMemory(page.Bytes);
        }
    }

    static bool IsRetryable(Exception ex) =>
        ex is NpgsqlException or TimeoutException or HttpRequestException
        || ex is OperationCanceledException;

    static string SafeErrorCode(Exception ex) => ex switch
    {
        InvalidDataException => "invalid_stored_document",
        HttpRequestException => "vision_provider_transport",
        TimeoutException or OperationCanceledException => "vision_provider_timeout",
        NpgsqlException => "database_temporary_failure",
        _ => "document_processing_error"
    };

    static async Task<Started?> StartAsync(NpgsqlDataSource source, Claimed item, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(@"
            select attempt_id, attempt_number, direction, source_kind
              from integrations.start_document_processing(@tenant, @document, @lease)", conn);
        command.Parameters.AddWithValue("tenant", item.TenantId);
        command.Parameters.AddWithValue("document", item.DocumentId);
        command.Parameters.AddWithValue("lease", item.LeaseToken);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new Started(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3));
    }

    static async Task<List<SourcePage>> LoadPagesAsync(
        NpgsqlDataSource source, Claimed item, Guid attemptId, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(@"
            select page_number, file_name, content_type, size_bytes, content_sha256, content
              from integrations.load_document_processing_pages(@tenant, @document, @lease, @attempt)", conn);
        command.Parameters.AddWithValue("tenant", item.TenantId);
        command.Parameters.AddWithValue("document", item.DocumentId);
        command.Parameters.AddWithValue("lease", item.LeaseToken);
        command.Parameters.AddWithValue("attempt", attemptId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<SourcePage>();
        while (await reader.ReadAsync(ct))
        {
            result.Add(new SourcePage(
                reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                reader.GetInt32(3), reader.GetString(4).Trim(), reader.GetFieldValue<byte[]>(5)));
        }
        return result;
    }

    static async Task FinishAsync(
        NpgsqlDataSource source,
        Claimed item,
        Started started,
        string finalState,
        string? provider,
        string? model,
        JsonObject? extractedData,
        JsonArray? warnings,
        string? safeErrorCode,
        int processingMs,
        DateTimeOffset? retryAt,
        CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(@"
            select integrations.finish_document_processing(
                @tenant, @document, @lease, @attempt, @state,
                @provider, @model, @data, @warnings, @error, @duration, @retryAt)", conn);
        command.Parameters.AddWithValue("tenant", item.TenantId);
        command.Parameters.AddWithValue("document", item.DocumentId);
        command.Parameters.AddWithValue("lease", item.LeaseToken);
        command.Parameters.AddWithValue("attempt", started.AttemptId);
        command.Parameters.AddWithValue("state", finalState);
        command.Parameters.AddWithValue("provider", (object?)provider ?? DBNull.Value);
        command.Parameters.AddWithValue("model", (object?)model ?? DBNull.Value);
        command.Parameters.Add("data", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = (object?)extractedData?.ToJsonString() ?? DBNull.Value;
        command.Parameters.Add("warnings", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = (object?)warnings?.ToJsonString() ?? DBNull.Value;
        command.Parameters.AddWithValue("error", (object?)safeErrorCode ?? DBNull.Value);
        command.Parameters.AddWithValue("duration", processingMs);
        command.Parameters.AddWithValue("retryAt", (object?)retryAt ?? DBNull.Value);
        var value = await command.ExecuteScalarAsync(ct);
        if (value is not true)
            throw new InvalidOperationException("The document processing lease expired before the result could be saved.");
    }

    static async Task FailBeforeStartAsync(
        NpgsqlDataSource source, Claimed item, string safeErrorCode, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            "select integrations.fail_document_processing_before_start(@tenant, @document, @lease, @code)", conn);
        command.Parameters.AddWithValue("tenant", item.TenantId);
        command.Parameters.AddWithValue("document", item.DocumentId);
        command.Parameters.AddWithValue("lease", item.LeaseToken);
        command.Parameters.AddWithValue("code", safeErrorCode);
        await command.ExecuteScalarAsync(ct);
    }

    static async Task FinishLegacyQueueAsync(
        NpgsqlDataSource source, Claimed item, DateTimeOffset? retryAt, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            "select integrations.finish_document_processing(@document, @lease, @retryAt)", conn);
        command.Parameters.AddWithValue("document", item.DocumentId);
        command.Parameters.AddWithValue("lease", item.LeaseToken);
        command.Parameters.AddWithValue("retryAt", (object?)retryAt ?? DBNull.Value);
        await command.ExecuteScalarAsync(ct);
    }
}
