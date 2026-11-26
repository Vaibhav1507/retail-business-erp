using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

public sealed record DocumentUploadPage(string FileName, string ContentType, byte[] Content, string Sha256);
public sealed record DocumentUploadItem(string SourceKind, IReadOnlyList<DocumentUploadPage> Pages);
public sealed record DocumentBatchUploadResult(Guid BatchId, bool Replayed, IReadOnlyList<DocumentUploadSummary> Documents);
public sealed record DocumentUploadSummary(Guid Id, string Direction, string SourceKind, string Status, int PageCount, int TotalSizeBytes, DateTimeOffset CreatedAt);

public sealed class DocumentIntakeService(TenantDb tenantDb)
{
    public async Task<DocumentBatchUploadResult> UploadAsync(
        Guid userId,
        Guid tenantId,
        string idempotencyKey,
        string direction,
        IReadOnlyList<DocumentUploadItem> documents,
        CancellationToken ct)
    {
        var requestHash = ComputeRequestHash(direction, documents);
        var batchId = Guid.NewGuid();
        await using var session = await tenantDb.BeginAsync(userId, tenantId, ct);

        await using (var insertBatch = session.Command(@"
            insert into documents.document_batch
                (tenant_id, id, direction, idempotency_key, request_hash, document_count, created_by)
            values (@tenant, @id, @direction, @key, @hash, @count, @actor)
            on conflict (tenant_id, idempotency_key) do nothing
            returning id"))
        {
            insertBatch.Parameters.AddWithValue("tenant", tenantId);
            insertBatch.Parameters.AddWithValue("id", batchId);
            insertBatch.Parameters.AddWithValue("direction", direction);
            insertBatch.Parameters.AddWithValue("key", idempotencyKey);
            insertBatch.Parameters.AddWithValue("hash", requestHash);
            insertBatch.Parameters.AddWithValue("count", documents.Count);
            insertBatch.Parameters.AddWithValue("actor", userId);
            var inserted = await insertBatch.ExecuteScalarAsync(ct);
            if (inserted is null)
            {
                await using var existing = session.Command(@"
                    select id, request_hash
                      from documents.document_batch
                     where tenant_id = @tenant and idempotency_key = @key");
                existing.Parameters.AddWithValue("tenant", tenantId);
                existing.Parameters.AddWithValue("key", idempotencyKey);
                await using var reader = await existing.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("The existing document batch could not be loaded.");
                var existingId = reader.GetGuid(0);
                var existingHash = reader.GetString(1).Trim();
                await reader.CloseAsync();
                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(existingHash),
                    Encoding.ASCII.GetBytes(requestHash)))
                    throw new DocumentIdempotencyConflictException();

                var summaries = await LoadBatchDocumentsAsync(session, tenantId, existingId, ct);
                await session.CommitAsync(ct);
                return new DocumentBatchUploadResult(existingId, true, summaries);
            }
        }

        var created = new List<DocumentUploadSummary>(documents.Count);
        foreach (var item in documents)
        {
            var documentId = Guid.NewGuid();
            var totalBytes = item.Pages.Sum(x => x.Content.Length);
            var documentHash = ComputeDocumentHash(item.Pages);
            await using (var insertDocument = session.Command(@"
                insert into documents.business_document
                    (tenant_id, id, batch_id, direction, source_kind, status, page_count,
                     total_size_bytes, content_sha256, created_by)
                values (@tenant, @id, @batch, @direction, @source, 'Queued', @pages,
                        @size, @hash, @actor)"))
            {
                insertDocument.Parameters.AddWithValue("tenant", tenantId);
                insertDocument.Parameters.AddWithValue("id", documentId);
                insertDocument.Parameters.AddWithValue("batch", batchId);
                insertDocument.Parameters.AddWithValue("direction", direction);
                insertDocument.Parameters.AddWithValue("source", item.SourceKind);
                insertDocument.Parameters.AddWithValue("pages", item.Pages.Count);
                insertDocument.Parameters.AddWithValue("size", totalBytes);
                insertDocument.Parameters.AddWithValue("hash", documentHash);
                insertDocument.Parameters.AddWithValue("actor", userId);
                await insertDocument.ExecuteNonQueryAsync(ct);
            }

            for (var index = 0; index < item.Pages.Count; index++)
            {
                var page = item.Pages[index];
                await using var insertPage = session.Command(@"
                    insert into documents.document_page
                        (tenant_id, id, document_id, page_number, file_name, content_type,
                         size_bytes, content_sha256, content)
                    values (@tenant, @id, @document, @number, @name, @type,
                            @size, @hash, @content)");
                insertPage.Parameters.AddWithValue("tenant", tenantId);
                insertPage.Parameters.AddWithValue("id", Guid.NewGuid());
                insertPage.Parameters.AddWithValue("document", documentId);
                insertPage.Parameters.AddWithValue("number", index + 1);
                insertPage.Parameters.AddWithValue("name", page.FileName);
                insertPage.Parameters.AddWithValue("type", page.ContentType);
                insertPage.Parameters.AddWithValue("size", page.Content.Length);
                insertPage.Parameters.AddWithValue("hash", page.Sha256);
                insertPage.Parameters.AddWithValue("content", page.Content);
                await insertPage.ExecuteNonQueryAsync(ct);
            }

            await using (var enqueue = session.Command(
                "select integrations.enqueue_document_processing(@tenant, @document, @actor)"))
            {
                enqueue.Parameters.AddWithValue("tenant", tenantId);
                enqueue.Parameters.AddWithValue("document", documentId);
                enqueue.Parameters.AddWithValue("actor", userId);
                await enqueue.ExecuteNonQueryAsync(ct);
            }

            created.Add(new DocumentUploadSummary(
                documentId, direction, item.SourceKind, "Queued",
                item.Pages.Count, totalBytes, DateTimeOffset.UtcNow));
        }

        await session.CommitAsync(ct);
        return new DocumentBatchUploadResult(batchId, false, created);
    }

    static async Task<IReadOnlyList<DocumentUploadSummary>> LoadBatchDocumentsAsync(
        TenantSession session, Guid tenantId, Guid batchId, CancellationToken ct)
    {
        await using var command = session.Command(@"
            select id, direction, source_kind, status, page_count, total_size_bytes, created_at
              from documents.business_document
             where tenant_id = @tenant and batch_id = @batch
             order by created_at, id");
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("batch", batchId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<DocumentUploadSummary>();
        while (await reader.ReadAsync(ct))
            result.Add(new DocumentUploadSummary(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6)));
        return result;
    }

    static string ComputeRequestHash(string direction, IReadOnlyList<DocumentUploadItem> documents)
    {
        var builder = new StringBuilder(direction);
        foreach (var document in documents)
        {
            builder.Append('|').Append(document.SourceKind);
            foreach (var page in document.Pages)
                builder.Append('|').Append(page.Sha256);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    static string ComputeDocumentHash(IReadOnlyList<DocumentUploadPage> pages)
    {
        var canonical = string.Join("|", pages.Select(x => x.Sha256));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

public sealed class DocumentIdempotencyConflictException : Exception { }
