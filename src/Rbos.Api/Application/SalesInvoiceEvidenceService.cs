using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using Rbos.Api.Persistence;
using Rbos.Domain;

namespace Rbos.Api.Application;

/// <summary>
/// Sales-only synchronous source-bill persistence. Deliberately does not use
/// DocumentIntakeService or enqueue the generic document processing worker.
/// </summary>
public sealed class SalesInvoiceEvidenceService(TenantDb db, ILogger<SalesInvoiceEvidenceService> logger)
{
    public async Task<Guid> SaveAsync(
        Guid userId,
        Guid tenantId,
        string mime,
        byte[] original,
        string? requestedFileName,
        CancellationToken cancellationToken)
    {
        if (original.Length is 0 or > 8 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(original), "Sales source bills must be between 1 byte and 8 MB.");
        if (!SalesInvoiceCaptureService.IsSupportedMime(mime) || !SalesInvoiceCaptureService.MatchesType(original, mime))
            throw new ArgumentException("The source bill MIME type or file signature is invalid.", nameof(mime));

        var documentId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var idempotencyKey = "sales-bill-" + Guid.NewGuid().ToString("N");
        var digest = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant();
        var fileName = SafeFileName(requestedFileName, mime);
        var timestamp = DateTime.UtcNow;

        await using var session = await db.BeginAsync(userId, tenantId, cancellationToken);
        await using (var batch = session.Command(@"
            insert into documents.document_batch
              (tenant_id, id, direction, idempotency_key, request_hash, document_count, created_by)
            values (@tenant, @id, 'Sale', @key, @hash, 1, @actor)"))
        {
            batch.Parameters.AddWithValue("tenant", tenantId);
            batch.Parameters.AddWithValue("id", batchId);
            batch.Parameters.AddWithValue("key", idempotencyKey);
            batch.Parameters.AddWithValue("hash", digest);
            batch.Parameters.AddWithValue("actor", userId);
            await batch.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var document = session.Command(@"
            insert into documents.business_document
              (tenant_id, id, batch_id, direction, source_kind, status, page_count,
               total_size_bytes, content_sha256, created_by, created_at, updated_at)
            values (@tenant, @id, @batch, 'Sale', 'HandwrittenBill', 'NeedsReview', 1,
                    @size, @hash, @actor, @created, @created)"))
        {
            document.Parameters.AddWithValue("tenant", tenantId);
            document.Parameters.AddWithValue("id", documentId);
            document.Parameters.AddWithValue("batch", batchId);
            document.Parameters.AddWithValue("size", original.Length);
            document.Parameters.AddWithValue("hash", digest);
            document.Parameters.AddWithValue("actor", userId);
            document.Parameters.AddWithValue("created", timestamp);
            await document.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var page = session.Command(@"
            insert into documents.document_page
              (tenant_id, id, document_id, page_number, file_name, content_type,
               size_bytes, content_sha256, content)
            values (@tenant, @id, @document, 1, @name, @mime, @size, @hash, @content)"))
        {
            page.Parameters.AddWithValue("tenant", tenantId);
            page.Parameters.AddWithValue("id", pageId);
            page.Parameters.AddWithValue("document", documentId);
            page.Parameters.AddWithValue("name", fileName);
            page.Parameters.AddWithValue("mime", mime);
            page.Parameters.AddWithValue("size", original.Length);
            page.Parameters.AddWithValue("hash", digest);
            page.Parameters.Add("content", NpgsqlDbType.Bytea).Value = original;
            await page.ExecuteNonQueryAsync(cancellationToken);
        }

        await session.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Sales handwritten source bill retained. Document {DocumentId}, size {SizeBytes}, MIME {Mime}, content hash {ContentHashPrefix}",
            documentId, original.Length, mime, digest[..12]);
        return documentId;
    }

    public async Task<ServiceResult<object>> DiscardAsync(
        Guid userId, Guid tenantId, Guid documentId, CancellationToken cancellationToken)
    {
        await using var session = await db.BeginAsync(userId, tenantId, cancellationToken);
        await using var command = session.Command(@"
            update documents.business_document
               set status = 'Cancelled', updated_at = now()
             where id = @id
               and direction = 'Sale'
               and source_kind = 'HandwrittenBill'
               and status in ('NeedsReview', 'FailedNeedsAction')
            returning id");
        command.Parameters.AddWithValue("id", documentId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null)
            return ServiceResult<object>.Fail("This unposted sales source bill was not found or can no longer be discarded.", 404);
        await session.CommitAsync(cancellationToken);
        return ServiceResult<object>.Ok(new { documentId, status = "Cancelled" });
    }

    static bool MatchesSignature(byte[] bytes, string mime) => mime switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "image/webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        "application/pdf" => bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
        _ => false
    };

    static string SafeFileName(string? requested, string mime)
    {
        var fallback = mime switch
        {
            "application/pdf" => "handwritten-sales-bill.pdf",
            "image/png" => "handwritten-sales-bill.png",
            "image/webp" => "handwritten-sales-bill.webp",
            _ => "handwritten-sales-bill.jpg"
        };
        var fileName = Path.GetFileName((requested ?? "").Replace('\\', '/')).Trim();
        fileName = new string(fileName.Where(character => !char.IsControl(character)).Take(180).ToArray());
        return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
    }
}
