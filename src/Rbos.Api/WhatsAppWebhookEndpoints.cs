using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rbos.Api.Persistence;

namespace Rbos.Api;

public static class WhatsAppWebhookEndpoints
{
    const int MaxWebhookBytes = 1024 * 1024;

    public static void Map(WebApplication app)
    {
        app.MapGet("/webhooks/meta/whatsapp", (HttpRequest request, IConfiguration config) =>
        {
            var expected = config["WhatsApp:VerifyToken"];
            var mode = request.Query["hub.mode"].ToString();
            var supplied = request.Query["hub.verify_token"].ToString();
            var challenge = request.Query["hub.challenge"].ToString();
            if (mode != "subscribe" || string.IsNullOrWhiteSpace(expected) || !FixedEquals(expected, supplied))
                return Results.Unauthorized();
            return Results.Text(challenge, "text/plain");
        }).AllowAnonymous();

        app.MapPost("/webhooks/meta/whatsapp", async (HttpRequest request, IConfiguration config, TenantDb db, CancellationToken ct) =>
        {
            var secret = config["WhatsApp:AppSecret"];
            var phoneNumberId = config["WhatsApp:PhoneNumberId"];
            var tenantText = config["WhatsApp:PilotTenantId"];
            var actorText = config["WhatsApp:PilotActorUserId"];
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(phoneNumberId)
                || !Guid.TryParse(tenantText, out var tenantId) || !Guid.TryParse(actorText, out var actorUserId))
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            if (request.ContentLength is > MaxWebhookBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            await using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, ct);
            if (buffer.Length > MaxWebhookBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            var body = buffer.ToArray();
            if (!VerifySignature(secret, request.Headers["X-Hub-Signature-256"].ToString(), body))
                return Results.Unauthorized();

            try
            {
                using var json = JsonDocument.Parse(body);
                if (!json.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                    return Results.Ok();
                await using var session = await db.BeginAsync(actorUserId, tenantId, ct);
                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array) continue;
                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var value)) continue;
                        if (!value.TryGetProperty("metadata", out var metadata)
                            || !metadata.TryGetProperty("phone_number_id", out var configuredId)
                            || !string.Equals(configuredId.GetString(), phoneNumberId, StringComparison.Ordinal))
                            continue;
                        if (!value.TryGetProperty("statuses", out var statuses) || statuses.ValueKind != JsonValueKind.Array) continue;
                        foreach (var status in statuses.EnumerateArray())
                        {
                            var messageId = GetString(status, "id");
                            var providerStatus = GetString(status, "status");
                            if (string.IsNullOrWhiteSpace(messageId) || providerStatus is null) continue;
                            var mapped = providerStatus switch
                            {
                                "sent" => "Sent",
                                "delivered" => "Delivered",
                                "read" => "Read",
                                "failed" => "Failed",
                                _ => null
                            };
                            if (mapped is null) continue;
                            var timestampText = GetString(status, "timestamp");
                            if (!long.TryParse(timestampText, out var unix)) continue;
                            var statusAt = DateTimeOffset.FromUnixTimeSeconds(unix);
                            string? error = null;
                            if (status.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
                                error = "Meta delivery error " + GetString(errors[0], "code");
                            await using var command = session.Command(@"update sales.invoice_delivery
                                set status=@status, provider_status_at=@at,
                                    last_error=case when @status='Failed' then left(@error,300) else null end,
                                    updated_at=now()
                                where tenant_id=@tenant and channel='WhatsApp' and provider_message_id=@message
                                  and (provider_status_at is null or @at >= provider_status_at)
                                  and (case @status when 'Sent' then 1 when 'Delivered' then 2 when 'Read' then 3 when 'Failed' then 4 else 0 end)
                                      >=
                                      (case status when 'Accepted' then 0 when 'Sent' then 1 when 'Delivered' then 2 when 'Read' then 3 when 'Failed' then 4 else -1 end)");
                            command.Parameters.AddWithValue("tenant", tenantId);
                            command.Parameters.AddWithValue("message", messageId);
                            command.Parameters.AddWithValue("status", mapped);
                            command.Parameters.AddWithValue("at", statusAt);
                            command.Parameters.AddWithValue("error", (object?)error ?? DBNull.Value);
                            await command.ExecuteNonQueryAsync(ct);
                        }
                    }
                }
                await session.CommitAsync(ct);
                return Results.Ok();
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }
        }).AllowAnonymous();
    }

    static string? GetString(JsonElement value, string name)
        => value.TryGetProperty(name, out var item) ? item.ToString() : null;

    static bool VerifySignature(string secret, string header, byte[] body)
    {
        if (!header.StartsWith("sha256=", StringComparison.Ordinal)) return false;
        byte[] supplied;
        try { supplied = Convert.FromHexString(header[7..]); }
        catch (FormatException) { return false; }
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    static bool FixedEquals(string expected, string supplied)
    {
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(supplied);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
