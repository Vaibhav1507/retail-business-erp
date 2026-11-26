using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Npgsql;
using Rbos.Api.Application;
using Rbos.Api.Persistence;
using Rbos.Domain;

namespace Rbos.Api;

public sealed record DocumentBatchManifest(string Direction, List<DocumentManifest> Documents);
public sealed record DocumentManifest(string SourceKind, List<string> Pages);

public static class DocumentEndpoints
{
    const int MaxDocumentsPerBatch = 10;
    const int MaxPagesPerDocument = 6;
    const int MaxPageBytes = 8 * 1024 * 1024;
    const int MaxBatchBytes = 24 * 1024 * 1024;
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf"
    };
    static readonly HashSet<string> AllowedSourceKinds = new(StringComparer.Ordinal)
    {
        "HandwrittenBill", "PrintedInvoice", "ThermalReceipt", "Other"
    };

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/documents/batches", UploadBatch).RequireAuthorization();
        api.MapGet("/documents", ListDocuments).RequireAuthorization();
        api.MapGet("/documents/{id:guid}", GetDocument).RequireAuthorization();
        api.MapGet("/documents/{id:guid}/pages/{pageNumber:int}", GetPage).RequireAuthorization();
    }

    static async Task<IResult> UploadBatch(
        HttpRequest request,
        ClaimsPrincipal principal,
        TenantDb db,
        DocumentIntakeService intake,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
            return ApiResults.Fail(415, "Upload documents using multipart form data.");

        var idempotencyKey = request.Headers["Idempotency-Key"].ToString();
        if (idempotencyKey.Length is < 8 or > 100)
            return ApiResults.Fail(400, "Idempotency-Key header (8–100 characters) is required.");

        IFormCollection form;
        try { form = await request.ReadFormAsync(ct); }
        catch (InvalidDataException) { return ApiResults.Fail(400, "The upload form is invalid or too large."); }

        var manifestJson = form["manifest"].ToString();
        if (string.IsNullOrWhiteSpace(manifestJson) || manifestJson.Length > 32 * 1024)
            return ApiResults.Fail(400, "A valid document manifest is required.");

        DocumentBatchManifest? manifest;
        try { manifest = JsonSerializer.Deserialize<DocumentBatchManifest>(manifestJson, JsonOptions); }
        catch (JsonException) { return ApiResults.Fail(400, "The document manifest could not be read."); }

        if (manifest is null || (manifest.Direction != "Sale" && manifest.Direction != "Purchase"))
            return ApiResults.Fail(400, "Choose Sales bills or Purchase bills before uploading.");
        if (manifest.Documents is null || manifest.Documents.Count is < 1 or > MaxDocumentsPerBatch)
            return ApiResults.Fail(400, $"Upload between 1 and {MaxDocumentsPerBatch} bills in one batch.");

        var userId = UserId(principal);
        var role = await db.FindRoleAsync(userId, ct);
        var requiredPermission = manifest.Direction == "Sale" ? Permissions.SalesCreate : Permissions.PurchasesManage;
        if (!Permissions.Has(role, requiredPermission))
            return ApiResults.Fail(403, "You do not have permission to upload this type of bill.");

        var filesByField = new Dictionary<string, IFormFile>(StringComparer.Ordinal);
        foreach (var file in form.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Name) || !filesByField.TryAdd(file.Name, file))
                return ApiResults.Fail(400, "Each uploaded page must have a unique field name.");
        }

        var referencedFields = new HashSet<string>(StringComparer.Ordinal);
        var uploads = new List<DocumentUploadItem>(manifest.Documents.Count);
        var batchBytes = 0;

        foreach (var document in manifest.Documents)
        {
            if (document is null || !AllowedSourceKinds.Contains(document.SourceKind)
                || document.Pages is null || document.Pages.Count is < 1 or > MaxPagesPerDocument)
                return ApiResults.Fail(400, "Each bill needs a supported type and between 1 and 6 pages.");

            var pages = new List<DocumentUploadPage>(document.Pages.Count);
            foreach (var field in document.Pages)
            {
                if (string.IsNullOrWhiteSpace(field) || field.Length > 64
                    || !referencedFields.Add(field) || !filesByField.TryGetValue(field, out var file))
                    return ApiResults.Fail(400, "The document manifest contains a missing or repeated page.");

                if (file.Length is <= 0 or > MaxPageBytes)
                    return ApiResults.Fail(413, "Each bill page must be between 1 byte and 8 MB.");

                var contentType = (file.ContentType?.Split(';')[0].Trim() ?? "").ToLowerInvariant();
                if (!AllowedContentTypes.Contains(contentType))
                    return ApiResults.Fail(415, "Choose JPEG, PNG, WebP or PDF files.");

                var pageSize = checked((int)file.Length);
                byte[] validationBuffer = new byte[pageSize];
                string hash;
                try
                {
                    await using var stream = file.OpenReadStream();
                    await stream.ReadExactlyAsync(validationBuffer.AsMemory(), ct);
                    if (stream.ReadByte() != -1 || !HasValidSignature(validationBuffer, contentType))
                        return ApiResults.Fail(400, "A bill page is damaged or its file type does not match the content.");
                    hash = Convert.ToHexString(SHA256.HashData(validationBuffer)).ToLowerInvariant();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(validationBuffer);
                }

                batchBytes += pageSize;
                if (batchBytes > MaxBatchBytes)
                    return ApiResults.Fail(413, "A batch can contain up to 24 MB of bill pages.");

                var fileName = SafeFileName(file.FileName);
                // Read the ASP.NET buffered form file again later, one page at a time,
                // while the metadata and queue row are committed in the same transaction.
                pages.Add(new DocumentUploadPage(fileName, contentType, pageSize, hash, file.OpenReadStream));
            }
            uploads.Add(new DocumentUploadItem(document.SourceKind, pages));
        }

        if (referencedFields.Count != filesByField.Count)
            return ApiResults.Fail(400, "Every uploaded page must belong to exactly one bill.");

        var tenantId = await db.FindTenantAsync(userId, ct);
        if (tenantId is null)
            return ApiResults.Fail(409, "Set up a business before uploading bills.");

        try
        {
            var result = await intake.UploadAsync(
                userId, tenantId.Value, idempotencyKey, manifest.Direction, uploads, ct);
            return Results.Ok(new
            {
                batchId = result.BatchId,
                replayed = result.Replayed,
                documents = result.Documents
            });
        }
        catch (DocumentIdempotencyConflictException)
        {
            return ApiResults.Fail(409, "This upload key was already used for a different batch. Start a new upload.");
        }
    }

    static async Task<IResult> ListDocuments(
        string? direction,
        string? status,
        int? limit,
        ClaimsPrincipal principal,
        TenantDb db,
        CancellationToken ct)
    {
        if (direction is not null && direction is not ("Sale" or "Purchase"))
            return ApiResults.Fail(400, "Direction must be Sale or Purchase.");
        var allowedStatuses = new HashSet<string>(StringComparer.Ordinal)
        {
            "Queued", "Processing", "NeedsReview", "ReadyToPost", "Posted",
            "FailedRetryable", "FailedNeedsAction", "DuplicateReview", "Rejected", "Cancelled"
        };
        if (status is not null && !allowedStatuses.Contains(status))
            return ApiResults.Fail(400, "Document status is not recognised.");

        var userId = UserId(principal);
        var role = await db.FindRoleAsync(userId, ct);
        var canReadSales = CanReadDirection(role, "Sale");
        var canReadPurchases = CanReadDirection(role, "Purchase");
        if (direction == "Sale" && !canReadSales || direction == "Purchase" && !canReadPurchases)
            return ApiResults.Fail(403, "You do not have permission to view this type of bill.");
        var tenantId = await db.FindTenantAsync(userId, ct);
        if (tenantId is null) return Results.Ok(Array.Empty<object>());

        await using var session = await db.BeginAsync(userId, tenantId, ct);
        await using var command = session.Command(@"
            select id, batch_id, direction, source_kind, status, page_count, total_size_bytes, created_at
              from documents.business_document
             where ((direction = 'Sale' and @can_sales) or (direction = 'Purchase' and @can_purchases))
               and (@direction is null or direction = @direction)
               and (@status is null or status = @status)
             order by created_at desc, id
             limit @limit");
        command.Parameters.AddWithValue("can_sales", canReadSales);
        command.Parameters.AddWithValue("can_purchases", canReadPurchases);
        command.Parameters.AddWithValue("direction", NpgsqlTypes.NpgsqlDbType.Text, (object?)direction ?? DBNull.Value);
        command.Parameters.AddWithValue("status", NpgsqlTypes.NpgsqlDbType.Text, (object?)status ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", Math.Clamp(limit ?? 50, 1, 100));
        await using var reader = await command.ExecuteReaderAsync(ct);
        var documents = new List<object>();
        while (await reader.ReadAsync(ct))
            documents.Add(new
            {
                id = reader.GetGuid(0),
                batchId = reader.GetGuid(1),
                direction = reader.GetString(2),
                sourceKind = reader.GetString(3),
                status = reader.GetString(4),
                pageCount = reader.GetInt32(5),
                totalSizeBytes = reader.GetInt32(6),
                createdAt = new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero)
            });
        return Results.Ok(documents);
    }

    static async Task<IResult> GetDocument(
        Guid id,
        ClaimsPrincipal principal,
        TenantDb db,
        CancellationToken ct)
    {
        var userId = UserId(principal);
        var role = await db.FindRoleAsync(userId, ct);
        var tenantId = await db.FindTenantAsync(userId, ct);
        if (tenantId is null) return Results.NotFound();

        await using var session = await db.BeginAsync(userId, tenantId, ct);
        await using var command = session.Command(@"
            select id, batch_id, direction, source_kind, status, page_count, total_size_bytes, content_sha256, created_at
              from documents.business_document where id = @id");
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return Results.NotFound();
        var documentDirection = reader.GetString(2);
        if (!CanReadDirection(role, documentDirection)) return Results.NotFound();
        var document = new
        {
            id = reader.GetGuid(0),
            batchId = reader.GetGuid(1),
            direction = documentDirection,
            sourceKind = reader.GetString(3),
            status = reader.GetString(4),
            pageCount = reader.GetInt32(5),
            totalSizeBytes = reader.GetInt32(6),
            contentSha256 = reader.GetString(7).Trim(),
            createdAt = new DateTimeOffset(reader.GetDateTime(8), TimeSpan.Zero)
        };
        await reader.CloseAsync();

        await using var pagesCommand = session.Command(@"
            select page_number, file_name, content_type, size_bytes, content_sha256
              from documents.document_page
             where document_id = @id
             order by page_number");
        pagesCommand.Parameters.AddWithValue("id", id);
        await using var pagesReader = await pagesCommand.ExecuteReaderAsync(ct);
        var pages = new List<object>();
        while (await pagesReader.ReadAsync(ct))
            pages.Add(new
            {
                pageNumber = pagesReader.GetInt32(0),
                fileName = pagesReader.GetString(1),
                contentType = pagesReader.GetString(2),
                sizeBytes = pagesReader.GetInt32(3),
                sha256 = pagesReader.GetString(4).Trim()
            });

        await using var historyCommand = session.Command(@"
            select status, actor_user_id, occurred_at
              from documents.document_status_history
             where document_id = @id
             order by occurred_at, id");
        historyCommand.Parameters.AddWithValue("id", id);
        await using var historyReader = await historyCommand.ExecuteReaderAsync(ct);
        var statusHistory = new List<object>();
        while (await historyReader.ReadAsync(ct))
            statusHistory.Add(new
            {
                status = historyReader.GetString(0),
                actorUserId = historyReader.IsDBNull(1) ? (Guid?)null : historyReader.GetGuid(1),
                occurredAt = new DateTimeOffset(historyReader.GetDateTime(2), TimeSpan.Zero)
            });
        return Results.Ok(new { document, pages, statusHistory });
    }

    static async Task<IResult> GetPage(
        Guid id,
        int pageNumber,
        ClaimsPrincipal principal,
        TenantDb db,
        CancellationToken ct)
    {
        if (pageNumber is < 1 or > MaxPagesPerDocument) return Results.NotFound();
        var userId = UserId(principal);
        var role = await db.FindRoleAsync(userId, ct);
        var tenantId = await db.FindTenantAsync(userId, ct);
        if (tenantId is null) return Results.NotFound();

        await using var session = await db.BeginAsync(userId, tenantId, ct);
        await using var command = session.Command(@"
            select p.content, p.content_type, d.direction
              from documents.document_page p
              join documents.business_document d
                on d.tenant_id = p.tenant_id and d.id = p.document_id
             where p.document_id = @id and p.page_number = @number");
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("number", pageNumber);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return Results.NotFound();
        if (!CanReadDirection(role, reader.GetString(2))) return Results.NotFound();
        var content = (byte[])reader[0];
        var contentType = reader.GetString(1);
        return Results.File(content, contentType);
    }

    static bool CanReadDirection(string? role, string direction)
    {
        var financeOrAudit = Permissions.Has(role, Permissions.FinanceManage)
            || Permissions.Has(role, Permissions.AuditView);
        return direction == "Sale"
            ? financeOrAudit || Permissions.Has(role, Permissions.SalesCreate)
            : financeOrAudit || Permissions.Has(role, Permissions.PurchasesManage);
    }

    static Guid UserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new UnauthorizedAccessException("Authenticated user identity is missing.");
    }

    static string SafeFileName(string? value)
    {
        var name = Path.GetFileName((value ?? "").Replace('\\', '/')).Trim();
        var safe = new string(name.Where(c => !char.IsControl(c)).Take(180).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "bill-page" : safe;
    }

    static bool HasValidSignature(byte[] bytes, string contentType)
    {
        if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
            return bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        if (contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
            return bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        if (contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
            return bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            return bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        return false;
    }
}
