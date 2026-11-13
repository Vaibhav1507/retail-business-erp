using System.Globalization;
using System.Text.Json;
using Npgsql;
using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

public static class WhatsAppInvoiceWorker
{
    sealed record Claimed(Guid DeliveryId, Guid TenantId, Guid SaleId, Guid ActorUserId, Guid LeaseToken, int AttemptCount);
    sealed record InvoiceJob(JsonDocument Snapshot, string? Recipient, string ConsentStatus);

    public static async Task RunAsync(NpgsqlDataSource dataSource, NpgsqlDataSource queueDataSource, IConfiguration config, ILogger logger, CancellationToken ct)
    {
        var db = new TenantDb(dataSource);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        var accessToken = config["WhatsApp:AccessToken"];
        var phoneNumberId = config["WhatsApp:PhoneNumberId"];
        var template = config["WhatsApp:TemplateName"];
        var language = config["WhatsApp:TemplateLanguage"] ?? "en";
        var apiVersion = config["WhatsApp:ApiVersion"];
        var pilotTenantConfigured = Guid.TryParse(config["WhatsApp:PilotTenantId"], out var pilotTenantId);
        var configured = pilotTenantConfigured && !string.IsNullOrWhiteSpace(accessToken)
            && !string.IsNullOrWhiteSpace(phoneNumberId)
            && !string.IsNullOrWhiteSpace(template) && !string.IsNullOrWhiteSpace(apiVersion);
        var meta = configured
            ? new MetaWhatsAppCloudClient(http, apiVersion!, phoneNumberId!, accessToken!, template!, language)
            : null;

        while (!ct.IsCancellationRequested)
        {
            var claimed = await ClaimAsync(queueDataSource, ct);
            if (claimed.Count == 0) break;
            foreach (var item in claimed)
            {
                ct.ThrowIfCancellationRequested();
                var messageSendStarted = false;
                var messageAccepted = false;
                try
                {
                    if (!pilotTenantConfigured || item.TenantId != pilotTenantId)
                    {
                        await UpdateDeliveryAsync(db, item, "WaitingForSetup", null, "No WhatsApp sender is configured for this business.", ct);
                        await FinishQueueAsync(queueDataSource, item, null, ct);
                        continue;
                    }
                    var job = await LoadJobAsync(db, item, ct);
                    if (job is null) { await FinishQueueAsync(queueDataSource, item, null, ct); continue; }
                    using (job.Snapshot)
                    {
                        if (job.ConsentStatus != "OptedIn")
                        {
                            var blocked = job.ConsentStatus == "OptedOut" ? "Suppressed" : "ConsentRequired";
                            await UpdateDeliveryAsync(db, item, blocked, null, "Customer permission is not active.", ct);
                            await FinishQueueAsync(queueDataSource, item, null, ct);
                            continue;
                        }
                        var recipient = NormalizeIndianPhone(job.Recipient);
                        if (string.IsNullOrWhiteSpace(job.Recipient))
                        {
                            await UpdateDeliveryAsync(db, item, "NoRecipient", null, null, ct);
                            await FinishQueueAsync(queueDataSource, item, null, ct);
                            continue;
                        }
                        if (recipient is null)
                        {
                            await UpdateDeliveryAsync(db, item, "Failed", null, "Customer phone number is not a valid Indian mobile number.", ct);
                            await FinishQueueAsync(queueDataSource, item, null, ct);
                            continue;
                        }
                        if (!configured)
                        {
                            await UpdateDeliveryAsync(db, item, "WaitingForSetup", null, "WhatsApp Cloud API is not configured.", ct);
                            await FinishQueueAsync(queueDataSource, item, null, ct);
                            continue;
                        }

                        try
                        {
                            var pdf = InvoicePdfRenderer.Render(job.Snapshot.RootElement);
                            var invoiceNo = job.Snapshot.RootElement.GetProperty("sale").GetProperty("invoiceNo").GetString() ?? "invoice";
                            var mediaId = await meta!.UploadInvoicePdfAsync(pdf, invoiceNo, ct);
                            messageSendStarted = true;
                            var messageId = await meta.SendInvoiceTemplateAsync(recipient, mediaId, invoiceNo, ct);
                            await UpdateDeliveryAsync(db, item, "Accepted", messageId, null, ct);
                            messageAccepted = true;
                            await FinishQueueAsync(queueDataSource, item, null, ct);
                            logger.LogInformation("WhatsApp invoice accepted for delivery {DeliveryId}, provider message {MessageId}", item.DeliveryId, messageId);
                        }
                        catch (MetaWhatsAppApiException ex)
                        {
                            var retryable = ex.Retryable && item.AttemptCount < 6;
                            var retryAt = retryable
                                ? DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Max(0, item.AttemptCount - 1))))
                                : (DateTimeOffset?)null;
                            await UpdateDeliveryAsync(db, item, ex.NeedsReview ? "NeedsReview" : retryable ? "Retry" : "Failed", null, ex.SafeMessage, ct);
                            await FinishQueueAsync(queueDataSource, item, retryAt, ct);
                            logger.LogWarning("WhatsApp delivery {DeliveryId} failed (retryable={Retryable}, attempt={Attempt})", item.DeliveryId, retryable, item.AttemptCount);
                        }
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // HttpClient.Timeout is distinct from worker shutdown. An upload timeout
                    // is safe to retry; a message-send timeout has an ambiguous provider outcome.
                    var uncertain = messageSendStarted;
                    var retryAt = !uncertain && item.AttemptCount < 6
                        ? DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Max(0, item.AttemptCount - 1))))
                        : (DateTimeOffset?)null;
                    var status = uncertain ? "NeedsReview" : retryAt is null ? "Failed" : "Retry";
                    await UpdateDeliveryAsync(db, item, status, null,
                        uncertain ? "The provider response timed out. Check WhatsApp before retrying." : "The PDF upload timed out and can be retried.", ct);
                    await FinishQueueAsync(queueDataSource, item, retryAt, ct);
                }
                catch (HttpRequestException ex)
                {
                    var uncertain = messageSendStarted;
                    var retryAt = !uncertain && item.AttemptCount < 6
                        ? DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Max(0, item.AttemptCount - 1))))
                        : (DateTimeOffset?)null;
                    var status = uncertain ? "NeedsReview" : retryAt is null ? "Failed" : "Retry";
                    logger.LogWarning(ex, "WhatsApp delivery transport error for {DeliveryId} (message send started={SendStarted})", item.DeliveryId, messageSendStarted);
                    await UpdateDeliveryAsync(db, item, status, null,
                        uncertain ? "The provider response was not received. Check WhatsApp before retrying." : "The PDF upload request could not be completed.", ct);
                    await FinishQueueAsync(queueDataSource, item, retryAt, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Unexpected invoice delivery worker error for {DeliveryId}", item.DeliveryId);
                    if (messageAccepted)
                    {
                        // Meta accepted the message and the delivery record is already committed.
                        // Do not downgrade Accepted just because retiring the queue lease failed.
                        try { await FinishQueueAsync(queueDataSource, item, null, ct); }
                        catch (Exception completionError) { logger.LogError(completionError, "Could not retire accepted invoice queue lease {DeliveryId}; it will expire automatically", item.DeliveryId); }
                        continue;
                    }
                    var retryAt = !messageSendStarted && item.AttemptCount < 6 ? DateTimeOffset.UtcNow.AddMinutes(2) : (DateTimeOffset?)null;
                    var status = messageSendStarted ? "NeedsReview" : retryAt is null ? "Failed" : "Retry";
                    try
                    {
                        await UpdateDeliveryAsync(db, item, status, null,
                            messageSendStarted ? "Message send may have reached WhatsApp. Check WhatsApp before retrying." : "Unexpected delivery processing error.", ct);
                        await FinishQueueAsync(queueDataSource, item, retryAt, ct);
                    }
                    catch (Exception completionError)
                    {
                        logger.LogError(completionError, "Could not complete invoice delivery lease {DeliveryId}; it will expire automatically", item.DeliveryId);
                    }
                }
            }
        }
    }

    static async Task<List<Claimed>> ClaimAsync(NpgsqlDataSource source, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("select delivery_id, tenant_id, sale_id, actor_user_id, lease_token, attempt_count from integrations.claim_invoice_deliveries(5,900)", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new List<Claimed>();
        while (await reader.ReadAsync(ct))
            result.Add(new Claimed(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetInt32(5)));
        return result;
    }

    static async Task<InvoiceJob?> LoadJobAsync(TenantDb db, Claimed item, CancellationToken ct)
    {
        await using var session = await db.BeginAsync(item.ActorUserId, item.TenantId, ct);
        await using var cmd = session.Command(@"select s.payload, c.phone, coalesce(c.whatsapp_invoice_consent_status,'Unknown'), d.status
            from sales.invoice_delivery d
            join sales.invoice_snapshot s on s.tenant_id=d.tenant_id and s.sale_id=d.sale_id
            join sales.sale x on x.tenant_id=d.tenant_id and x.id=d.sale_id
            left join sales.customer c on c.tenant_id=x.tenant_id and c.id=x.customer_id
            where d.tenant_id=@t and d.id=@d and d.sale_id=@s and d.channel='WhatsApp'");
        cmd.Parameters.AddWithValue("t", item.TenantId);
        cmd.Parameters.AddWithValue("d", item.DeliveryId);
        cmd.Parameters.AddWithValue("s", item.SaleId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var snapshot = JsonDocument.Parse(reader.GetString(0));
        var recipient = reader.IsDBNull(1) ? null : reader.GetString(1);
        var consent = reader.GetString(2);
        var currentStatus = reader.GetString(3);
        await reader.CloseAsync();
        if (currentStatus == "Processing")
        {
            snapshot.Dispose();
            await using var review = session.Command(@"update sales.invoice_delivery
                set status='NeedsReview', last_error='Previous attempt outcome is unknown. Check WhatsApp before retrying.', updated_at=now()
                where tenant_id=@t and id=@d");
            review.Parameters.AddWithValue("t", item.TenantId);
            review.Parameters.AddWithValue("d", item.DeliveryId);
            await review.ExecuteNonQueryAsync(ct);
            await session.CommitAsync(ct);
            return null;
        }
        if (currentStatus is not ("Pending" or "Retry")) { snapshot.Dispose(); return null; }
        await using var update = session.Command("update sales.invoice_delivery set status='Processing', attempt_count=attempt_count+1, updated_at=now() where tenant_id=@t and id=@d");
        update.Parameters.AddWithValue("t", item.TenantId);
        update.Parameters.AddWithValue("d", item.DeliveryId);
        await update.ExecuteNonQueryAsync(ct);
        await session.CommitAsync(ct);
        return new InvoiceJob(snapshot, recipient, consent);
    }

    static async Task UpdateDeliveryAsync(TenantDb db, Claimed item, string status, string? messageId, string? error, CancellationToken ct)
    {
        await using var session = await db.BeginAsync(item.ActorUserId, item.TenantId, ct);
        await using var cmd = session.Command(@"update sales.invoice_delivery
            set status=@st, provider_message_id=coalesce(@mid,provider_message_id), last_error=@err, updated_at=now()
            where tenant_id=@t and id=@d");
        cmd.Parameters.AddWithValue("st", status);
        cmd.Parameters.AddWithValue("mid", (object?)messageId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("err", (object?)error ?? DBNull.Value);
        cmd.Parameters.AddWithValue("t", item.TenantId);
        cmd.Parameters.AddWithValue("d", item.DeliveryId);
        await cmd.ExecuteNonQueryAsync(ct);
        await session.CommitAsync(ct);
    }

    static async Task FinishQueueAsync(NpgsqlDataSource source, Claimed item, DateTimeOffset? retryAt, CancellationToken ct)
    {
        await using var conn = await source.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("select integrations.finish_invoice_delivery(@d,@l,@r)", conn);
        cmd.Parameters.AddWithValue("d", item.DeliveryId);
        cmd.Parameters.AddWithValue("l", item.LeaseToken);
        cmd.Parameters.AddWithValue("r", (object?)retryAt ?? DBNull.Value);
        await cmd.ExecuteScalarAsync(ct);
    }

    static string? NormalizeIndianPhone(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.Length == 10 && digits[0] is >= '6' and <= '9') return "91" + digits;
        if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal) && digits[2] is >= '6' and <= '9') return digits;
        return null;
    }


}
