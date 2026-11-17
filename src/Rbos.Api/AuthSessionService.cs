using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;

namespace Rbos.Api;

public sealed record AuthDeviceSummary(
    Guid Id,
    string Label,
    string Browser,
    string Platform,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset LastAuthenticatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    bool IsCurrentDevice);

/// <summary>
/// Stores server-authoritative sessions and user-scoped browser-install registrations.
/// The device cookie is an opaque identifier for grouping metadata only; it is not an
/// authentication factor and is never accepted in place of the Identity session cookie.
/// </summary>
public sealed class AuthSessionService(NpgsqlDataSource dataSource, IConfiguration configuration)
{
    public const string SessionClaimType = "rbos.session_id";
    public const string DeviceCookieName = "rbos.device";
    private const int DeviceCookieDays = 365;

    public int AbsoluteSessionDays =>
        Math.Clamp(configuration.GetValue("Auth:MaxSessionAgeDays", 90), 1, 365);

    public async Task SignInAsync(
        HttpContext context,
        IdentityUser<Guid> user,
        SignInManager<IdentityUser<Guid>> signInManager,
        CancellationToken cancellationToken = default)
    {
        var deviceId = await RegisterOrUpdateDeviceAsync(context, user.Id, cancellationToken);
        var sessionId = Guid.NewGuid();

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = new NpgsqlCommand("""
            insert into identity.auth_session (id, user_id, device_id, authenticated_at, expires_at)
            values (@id, @user_id, @device_id, now(), now() + (@days * interval '1 day'))
            """, connection))
        {
            command.Parameters.AddWithValue("id", sessionId);
            command.Parameters.AddWithValue("user_id", user.Id);
            command.Parameters.AddWithValue("device_id", deviceId);
            command.Parameters.AddWithValue("days", AbsoluteSessionDays);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await RecordSecurityEventAsync(user.Id, deviceId, sessionId, "session.created", cancellationToken);

        try
        {
            await signInManager.SignInWithClaimsAsync(
                user,
                new AuthenticationProperties { IsPersistent = true, AllowRefresh = true },
                [new Claim(SessionClaimType, sessionId.ToString("D"))]);
        }
        catch
        {
            await RevokeAsync(sessionId, user.Id, CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> IsActiveAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            with valid_session as (
                select s.device_id
                from identity.auth_session s
                join identity.auth_device d on d.id = s.device_id and d.user_id = s.user_id
                where s.id = @id
                  and s.user_id = @user_id
                  and s.revoked_at is null
                  and s.authenticated_at <= now()
                  and s.expires_at > now()
                  and d.revoked_at is null
                  and d.expires_at > now()
            ),
            touched_device as (
                update identity.auth_device d
                set last_seen_at = now()
                from valid_session v
                where d.id = v.device_id
                  and d.last_seen_at < now() - interval '5 minutes'
                returning d.id
            )
            select exists (select 1 from valid_session)
            """, connection);
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("user_id", userId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task RevokeCurrentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var sessionValue = principal.FindFirstValue(SessionClaimType);
        var userValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sessionValue, out var sessionId) || !Guid.TryParse(userValue, out var userId))
            return;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update identity.auth_session
            set revoked_at = coalesce(revoked_at, now())
            where id = @id and user_id = @user_id
            returning device_id
            """, connection);
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("user_id", userId);
        var device = await command.ExecuteScalarAsync(cancellationToken);
        if (device is Guid deviceId)
            await RecordSecurityEventAsync(userId, deviceId, sessionId, "session.signed_out", cancellationToken);
    }

    public async Task<IReadOnlyList<AuthDeviceSummary>> GetDevicesAsync(
        Guid userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var currentSessionId = Guid.TryParse(principal.FindFirstValue(SessionClaimType), out var sessionId)
            ? sessionId
            : (Guid?)null;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            select d.id, d.device_label, d.browser, d.platform, d.created_at, d.last_seen_at,
                   d.last_authenticated_at, d.expires_at, d.revoked_at,
                   exists (
                       select 1 from identity.auth_session s
                       where s.id = @current_session_id
                         and s.user_id = @user_id
                         and s.device_id = d.id
                         and s.revoked_at is null
                   ) as is_current
            from identity.auth_device d
            where d.user_id = @user_id
            order by (d.revoked_at is null and d.expires_at > now()) desc, d.last_seen_at desc
            """, connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.Add(new NpgsqlParameter("current_session_id", NpgsqlDbType.Uuid)
        {
            Value = currentSessionId.HasValue ? currentSessionId.Value : DBNull.Value
        });

        var devices = new List<AuthDeviceSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            devices.Add(new AuthDeviceSummary(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                reader.GetBoolean(9)));
        }

        return devices;
    }

    public async Task<bool> RevokeDeviceAsync(
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var find = new NpgsqlCommand("""
            select revoked_at
            from identity.auth_device
            where id = @id and user_id = @user_id
            for update
            """, connection, transaction);
        find.Parameters.AddWithValue("id", deviceId);
        find.Parameters.AddWithValue("user_id", userId);
        var revokedAt = await find.ExecuteScalarAsync(cancellationToken);
        if (revokedAt is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (revokedAt is DBNull)
        {
            await using (var revokeDevice = new NpgsqlCommand("""
                update identity.auth_device
                set revoked_at = now()
                where id = @id and user_id = @user_id and revoked_at is null
                """, connection, transaction))
            {
                revokeDevice.Parameters.AddWithValue("id", deviceId);
                revokeDevice.Parameters.AddWithValue("user_id", userId);
                await revokeDevice.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var revokeSessions = new NpgsqlCommand("""
                update identity.auth_session
                set revoked_at = coalesce(revoked_at, now())
                where device_id = @device_id and user_id = @user_id and revoked_at is null
                """, connection, transaction))
            {
                revokeSessions.Parameters.AddWithValue("device_id", deviceId);
                revokeSessions.Parameters.AddWithValue("user_id", userId);
                await revokeSessions.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var audit = new NpgsqlCommand("""
                insert into identity.auth_security_event
                    (id, user_id, device_id, event_type, metadata)
                values (@id, @user_id, @device_id, 'device.revoked', '{}'::jsonb)
                """, connection, transaction))
            {
                audit.Parameters.AddWithValue("id", Guid.NewGuid());
                audit.Parameters.AddWithValue("user_id", userId);
                audit.Parameters.AddWithValue("device_id", deviceId);
                await audit.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<Guid> RegisterOrUpdateDeviceAsync(
        HttpContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        if (userAgent.Length > 512) userAgent = userAgent[..512];
        var (browser, platform) = DescribeClient(userAgent);
        var label = $"{browser} on {platform}";
        var token = GetDeviceToken(context);

        if (token is not null)
        {
            var tokenHash = HashDeviceToken(token);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var update = new NpgsqlCommand("""
                update identity.auth_device
                set last_seen_at = now(),
                    last_authenticated_at = now(),
                    expires_at = now() + (@days * interval '1 day'),
                    device_label = @label,
                    browser = @browser,
                    platform = @platform,
                    user_agent = @user_agent
                where id = (
                    select id from identity.auth_device
                    where user_id = @user_id
                      and device_key_hash = @key_hash
                      and revoked_at is null
                      and expires_at > now()
                    order by last_seen_at desc
                    limit 1
                )
                returning id
                """, connection);
            update.Parameters.AddWithValue("days", DeviceCookieDays);
            update.Parameters.AddWithValue("label", label);
            update.Parameters.AddWithValue("browser", browser);
            update.Parameters.AddWithValue("platform", platform);
            update.Parameters.AddWithValue("user_agent", string.IsNullOrEmpty(userAgent) ? DBNull.Value : userAgent);
            update.Parameters.AddWithValue("user_id", userId);
            update.Parameters.AddWithValue("key_hash", tokenHash);
            var existing = await update.ExecuteScalarAsync(cancellationToken);
            if (existing is Guid existingId)
            {
                context.Response.Cookies.Append(DeviceCookieName, token, DeviceCookieOptions(context));
                return existingId;
            }
        }

        if (token is null)
            token = CreateDeviceToken();

        // Renew the non-authenticating device identifier whenever the user signs in.
        context.Response.Cookies.Append(DeviceCookieName, token, DeviceCookieOptions(context));

        var newDeviceId = Guid.NewGuid();
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var insert = new NpgsqlCommand("""
            insert into identity.auth_device (
                id, user_id, device_key_hash, device_label, browser, platform, user_agent,
                created_at, last_seen_at, last_authenticated_at, expires_at
            )
            values (
                @id, @user_id, @key_hash, @label, @browser, @platform, @user_agent,
                now(), now(), now(), now() + (@days * interval '1 day')
            )
            """, connection))
        {
            insert.Parameters.AddWithValue("id", newDeviceId);
            insert.Parameters.AddWithValue("user_id", userId);
            insert.Parameters.AddWithValue("key_hash", HashDeviceToken(token));
            insert.Parameters.AddWithValue("label", label);
            insert.Parameters.AddWithValue("browser", browser);
            insert.Parameters.AddWithValue("platform", platform);
            insert.Parameters.AddWithValue("user_agent", string.IsNullOrEmpty(userAgent) ? DBNull.Value : userAgent);
            insert.Parameters.AddWithValue("days", DeviceCookieDays);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await RecordSecurityEventAsync(userId, newDeviceId, null, "device.registered", cancellationToken);
        return newDeviceId;
    }

    private static string? GetDeviceToken(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(DeviceCookieName, out var token)
            || string.IsNullOrWhiteSpace(token)
            || token.Length > 128)
            return null;
        return token;
    }

    private static string CreateDeviceToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] HashDeviceToken(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static CookieOptions DeviceCookieOptions(HttpContext context) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        IsEssential = true,
        Path = "/",
        Expires = DateTimeOffset.UtcNow.AddDays(DeviceCookieDays)
    };

    private static (string Browser, string Platform) DescribeClient(string userAgent)
    {
        var ua = userAgent;
        var browser = ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Microsoft Edge"
            : ua.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox"
            : ua.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ? "Chrome"
            : ua.Contains("Safari/", StringComparison.OrdinalIgnoreCase) ? "Safari"
            : "Unknown browser";
        var platform = ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android"
            : ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iOS"
            : ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows"
            : ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) || ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) ? "macOS"
            : ua.Contains("CrOS", StringComparison.OrdinalIgnoreCase) ? "ChromeOS"
            : ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux"
            : "Unknown platform";
        return (browser, platform);
    }

    public async Task RecordCredentialChangeAsync(
        Guid userId,
        ClaimsPrincipal? principal,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        Guid? sessionId = null;
        Guid? deviceId = null;
        if (principal is not null
            && Guid.TryParse(principal.FindFirstValue(SessionClaimType), out var currentSessionId))
        {
            sessionId = currentSessionId;
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand("""
                select device_id from identity.auth_session
                where id = @session_id and user_id = @user_id
                """, connection);
            command.Parameters.AddWithValue("session_id", currentSessionId);
            command.Parameters.AddWithValue("user_id", userId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is Guid currentDeviceId) deviceId = currentDeviceId;
        }

        await RecordSecurityEventAsync(userId, deviceId, sessionId, eventType, cancellationToken);
    }

    private async Task RecordSecurityEventAsync(
        Guid userId,
        Guid? deviceId,
        Guid? sessionId,
        string eventType,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            insert into identity.auth_security_event
                (id, user_id, device_id, session_id, event_type, metadata)
            values (@id, @user_id, @device_id, @session_id, @event_type, '{}'::jsonb)
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("device_id", deviceId.HasValue ? deviceId.Value : DBNull.Value);
        command.Parameters.AddWithValue("session_id", sessionId.HasValue ? sessionId.Value : DBNull.Value);
        command.Parameters.AddWithValue("event_type", eventType);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task RevokeAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update identity.auth_session
            set revoked_at = coalesce(revoked_at, now())
            where id = @id and user_id = @user_id
            returning device_id
            """, connection);
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("user_id", userId);
        var device = await command.ExecuteScalarAsync(cancellationToken);
        if (device is Guid deviceId)
            await RecordSecurityEventAsync(userId, deviceId, sessionId, "session.sign_in_failed", cancellationToken);
    }
}
