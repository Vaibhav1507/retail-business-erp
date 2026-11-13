using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

public static class WhatsAppInvoiceWorker
{
    sealed record Claimed(Guid DeliveryId, Guid TenantId, Guid SaleId, Guid ActorUserId, Guid LeaseToken, int AttemptCount);
    sealed record InvoiceJob(JsonDocument Snapshot, string? Recipient, string ConsentStatus);

    public static async Task RunAsync(NpgsqlDataSource dataSource, IConfiguration config, ILogger logger, CancellationToken ct)
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

        while (!ct.IsCancellationRequested)
        {
            var claimed = await ClaimAsync(dataSource, ct);
            if (claimed.Count == 0) break;
            foreach (var item in claimed)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (!pilotTenantConfigured || item.TenantId != pilotTenantId)
                    {
                        await UpdateDeliveryAsync(db, item, "WaitingForSetup", null, "No WhatsApp sender is configured for this business.", ct);
                        await FinishQueueAsync(dataSource, item, null, ct);
                        continue;
                    }
                    var job = await LoadJobAsync(db, item, ct);
                    if (job is null) { await FinishQueueAsync(dataSource, item, null, ct); continue; }
                    using (job.Snapshot)
                    {
                        if (job.ConsentStatus != "OptedIn")
                        {
                            var blocked = job.ConsentStatus == "OptedOut" ? "Suppressed" : "ConsentRequired";
                            await UpdateDeliveryAsync(db, item, blocked, null, "Customer permission is not active.", ct);
                            await FinishQueueAsync(dataSource, item, null, ct);
                            continue;
                        }
                        var recipient = NormalizeIndianPhone(job.Recipient);
                        if (string.IsNullOrWhiteSpace(job.Recipient))
                        {
                            await UpdateDeliveryAsync(db, item, "NoRecipient", null, null, ct);
                            await FinishQueueAsync(dataSource, item, null, ct);
                            continue;
                        }
                        if (recipient is null)
                        {
                            await UpdateDeliveryAsync(db, item, "Failed", null, "Customer phone number is not a valid Indian mobile number.", ct);
                            await FinishQueueAsync(dataSource, item, null, ct);
                            continue;
                        }
                        if (!configured)
                        {
                            await UpdateDeliveryAsync(db, item, "WaitingForSetup", null, "WhatsApp Cloud API is not configured.", ct);
                            await FinishQueueAsync(dataSource, item, null, ct);
                            continue;
                        }

                        try
                        {
                            var pdf = InvoicePdfRenderer.Render(job.Snapshot.RootElement);
                            var invoiceNo = job.Snapshot.RootElement.GetProperty("sale").GetProperty("invoiceNo").GetString() ?? "invoice";
                            var mediaId = await UploadPdfAsync(http, apiVersion, phoneNumberId!, accessToken!, pdf, invoiceNo, ct);
                            var messageId = await SendTemplateAsync(http, apiVersion, phoneNumberId!, accessToken!, template!, language, recipient, mediaId, invoiceNo, ct);
                            await UpdateDeliveryAsync(db, item, "Accepted", messageId, null, ct);
                            await FinishQueueAsync(dataSource, item, null, ct);
                            logger.LogInformation("WhatsApp invoice accepted for delivery {DeliveryId}, provider message {MessageId}", item.DeliveryId, messageId);
                        }
                        catch (MetaApiException ex)
                        {
                            var retryable = ex.Retryable && item.AttemptCount < 6;
                            var retryAt = retryable
                                ? DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Max(0, item.AttemptCount - 1))))
                                : (DateTimeOffset?)null;
                            await UpdateDeliveryAsync(db, item, ex.NeedsReview ? "NeedsReview" : retryable ? "Retry" : "Failed", null, ex.SafeMessage, ct);
                            await FinishQueueAsync(dataSource, item, retryAt, ct);
                            logger.LogWarning("WhatsApp delivery {DeliveryId} failed (retryable={Retryable}, attempt={Attempt})", item.DeliveryId, retryable, item.AttemptCount);
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex, "WhatsApp delivery outcome is uncertain for {DeliveryId}", item.DeliveryId);
                    await UpdateDeliveryAsync(db, item, "NeedsReview", null, "The provider response was not received. Check WhatsApp before retrying.", ct);
                    await FinishQueueAsync(dataSource, item, null, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Unexpected invoice delivery worker error for {DeliveryId}", item.DeliveryId);
                    var retryAt = item.AttemptCount < 6 ? DateTimeOffset.UtcNow.AddMinutes(2) : (DateTimeOffset?)null;
                    try
                    {
                        await UpdateDeliveryAsync(db, item, retryAt is null ? "Failed" : "Retry", null, "Unexpected delivery processing error.", ct);
                        await FinishQueueAsync(dataSource, item, retryAt, ct);
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
        await using var cmd = new NpgsqlCommand("select delivery_id, tenant_id, sale_id, actor_user_id, lease_token, attempt_count from integrations.claim_invoice_deliveries(10,180)", conn);
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
            await using var review = session.Command(@"update sales.invoice_delivery
                set status='NeedsReview', last_error='Previous attempt outcome is unknown. Check WhatsApp before retrying.', updated_at=now()
                where tenant_id=@t and id=@d");
            review.Parameters.AddWithValue("t", item.TenantId);
            review.Parameters.AddWithValue("d", item.DeliveryId);
            await review.ExecuteNonQueryAsync(ct);
            await session.CommitAsync(ct);
            return null;
        }
        if (currentStatus is not ("Pending" or "Retry")) return null;
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

    static async Task<string> UploadPdfAsync(HttpClient http, string version, string phoneNumberId, string token, byte[] pdf, string invoiceNo, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", SafeFileName(invoiceNo) + ".pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{version}/{phoneNumberId}/media") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw MetaApiException.FromResponse(response.StatusCode, body);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("id").GetString() ?? throw new MetaApiException(false, "Meta media upload did not return a media ID.");
    }

    static async Task<string> SendTemplateAsync(HttpClient http, string version, string phoneNumberId, string token, string template, string language, string recipient, string mediaId, string invoiceNo, CancellationToken ct)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = recipient,
            type = "template",
            template = new
            {
                name = template,
                language = new { code = language },
                components = new object[]
                {
                    new { type = "header", parameters = new object[] { new { type = "document", document = new { id = mediaId, filename = SafeFileName(invoiceNo) + ".pdf" } } } },
                    new { type = "body", parameters = new object[] { new { type = "text", text = invoiceNo } } }
                }
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{version}/{phoneNumberId}/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw MetaApiException.FromResponse(response.StatusCode, body);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("messages")[0].GetProperty("id").GetString()
            ?? throw new MetaApiException(false, "Meta send response did not contain a message ID.");
    }

    static string SafeFileName(string value)
    {
        var safe = new string(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').Take(40).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "invoice" : safe;
    }

    sealed class MetaApiException(bool retryable, string safeMessage, bool needsReview = false) : Exception(safeMessage)
    {
        public bool Retryable { get; } = retryable;
        public bool NeedsReview { get; } = needsReview;
        public string SafeMessage { get; } = safeMessage;

        public static MetaApiException FromResponse(HttpStatusCode status, string body)
        {
            var retryable = status == HttpStatusCode.TooManyRequests;
            var needsReview = (int)status >= 500;
            string message = "Meta WhatsApp API request failed (HTTP " + (int)status + ").";
            try
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var code))
                    message += " Provider error code: " + code.ToString() + ".";
            }
            catch (JsonException) { }
            return new MetaApiException(retryable, message, needsReview);
        }
    }
}
