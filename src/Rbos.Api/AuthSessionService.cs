using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace Rbos.Api;

/// <summary>
/// Stores server-authoritative login sessions. The protected cookie contains only
/// the opaque session ID; expiry and revocation are checked against PostgreSQL.
/// </summary>
public sealed class AuthSessionService(NpgsqlDataSource dataSource, IConfiguration configuration)
{
    public const string SessionClaimType = "rbos.session_id";

    public int AbsoluteSessionDays =>
        Math.Clamp(configuration.GetValue("Auth:MaxSessionAgeDays", 90), 1, 365);

    public async Task SignInAsync(
        HttpContext context,
        IdentityUser<Guid> user,
        SignInManager<IdentityUser<Guid>> signInManager,
        CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid();
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var command = new NpgsqlCommand("""
            insert into identity.auth_session (id, user_id, authenticated_at, expires_at)
            values (@id, @user_id, now(), now() + (@days * interval '1 day'))
            """, connection))
        {
            command.Parameters.AddWithValue("id", sessionId);
            command.Parameters.AddWithValue("user_id", user.Id);
            command.Parameters.AddWithValue("days", AbsoluteSessionDays);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            var principal = await signInManager.CreateUserPrincipalAsync(user);
            if (principal.Identity is not ClaimsIdentity identity)
                throw new InvalidOperationException("Identity did not create a claims identity.");

            identity.AddClaim(new Claim(SessionClaimType, sessionId.ToString("D")));
            await context.SignInAsync(
                IdentityConstants.ApplicationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });
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
            select exists (
                select 1
                from identity.auth_session
                where id = @id
                  and user_id = @user_id
                  and revoked_at is null
                  and authenticated_at <= now()
                  and expires_at > now()
            )
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

        await RevokeAsync(sessionId, userId, cancellationToken);
    }

    private async Task RevokeAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            update identity.auth_session
            set revoked_at = coalesce(revoked_at, now())
            where id = @id and user_id = @user_id
            """, connection);
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("user_id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
