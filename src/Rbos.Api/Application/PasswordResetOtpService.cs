using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace Rbos.Api.Application;

public interface IEmailOtpSender
{
    bool IsConfigured { get; }
    Task SendAsync(string email, string code, CancellationToken cancellationToken);
}

public sealed class SmtpEmailOtpSender(IConfiguration configuration) : IEmailOtpSender
{
    private string? Host => configuration["Email:Smtp:Host"];
    private string? FromAddress => configuration["Email:Smtp:FromAddress"];
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(FromAddress) &&
        !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Username"]) &&
        !string.IsNullOrWhiteSpace(configuration["Email:Smtp:Password"]);

    public async Task SendAsync(string email, string code, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InvalidOperationException("Email SMTP delivery is not configured.");
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(configuration["Email:Smtp:FromName"] ?? "Dhandha Mitra", FromAddress!));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Your Dhandha Mitra password reset code";
        message.Body = new TextPart("plain")
        {
            Text = $"Your password reset code is {code}. It expires in 10 minutes. If you did not request this, you can ignore this email."
        };

        var security = (configuration["Email:Smtp:Security"] ?? "StartTls").Trim() switch
        {
            "StartTls" => SecureSocketOptions.StartTls,
            "SslOnConnect" => SecureSocketOptions.SslOnConnect,
            _ => throw new InvalidOperationException("Email:Smtp:Security must be StartTls or SslOnConnect.")
        };
        using var client = new SmtpClient();
        client.Timeout = Math.Clamp(configuration.GetValue("Email:Smtp:TimeoutSeconds", 15), 5, 60) * 1000;
        await client.ConnectAsync(Host!, configuration.GetValue("Email:Smtp:Port", 587), security, cancellationToken);
        await client.AuthenticateAsync(configuration["Email:Smtp:Username"]!, configuration["Email:Smtp:Password"]!, cancellationToken);
        await client.SendAsync(new SmtpSendRequest(message), cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

/// <summary>Short-lived, single-use email OTP challenges for account password recovery.</summary>
public sealed class PasswordResetOtpService(
    NpgsqlDataSource dataSource,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    IEmailOtpSender sender,
    ILogger<PasswordResetOtpService> logger)
{
    private const int MaxAttempts = 5;
    private const int MaxPerHour = 5;
    private const string DevelopmentPepper = "development-only-password-reset-pepper";
    private string? Pepper => configuration["Auth:PasswordReset:Pepper"]
        ?? (environment.IsDevelopment() ? DevelopmentPepper : null);

    public bool IsEnabled => sender.IsConfigured && !string.IsNullOrWhiteSpace(Pepper);

    private string Hash(Guid id, Guid userId, string code)
    {
        var pepper = Pepper ?? throw new InvalidOperationException("Password reset pepper is not configured.");
        var payload = Encoding.UTF8.GetBytes($"{id:D}|{userId:D}|{code}");
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), payload));
    }

    public async Task SendAsync(IdentityUser<Guid> user, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(user.Email)) return;
        var id = Guid.NewGuid();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var hash = Hash(id, user.Id, code);

        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await using (var advisory = new NpgsqlCommand("select pg_advisory_xact_lock(hashtextextended(@key, 0))", connection, transaction))
            {
                advisory.Parameters.AddWithValue("key", user.Id.ToString("N"));
                await advisory.ExecuteNonQueryAsync(cancellationToken);
            }

            bool limited;
            await using (var limits = new NpgsqlCommand("""
                select count(*) filter (where created_at > now() - interval '1 minute'), count(*)
                  from identity.password_reset_otp
                 where user_id = @user_id and created_at > now() - interval '1 hour'
                """, connection, transaction))
            {
                limits.Parameters.AddWithValue("user_id", user.Id);
                await using var reader = await limits.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                limited = reader.GetInt64(0) > 0 || reader.GetInt64(1) >= MaxPerHour;
            }
            if (limited)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            await using (var expire = new NpgsqlCommand(
                "update identity.password_reset_otp set consumed_at = now() where user_id = @user_id and consumed_at is null",
                connection, transaction))
            {
                expire.Parameters.AddWithValue("user_id", user.Id);
                await expire.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var insert = new NpgsqlCommand("""
                insert into identity.password_reset_otp(id, user_id, code_hash, expires_at)
                values (@id, @user_id, @code_hash, now() + interval '10 minutes')
                """, connection, transaction))
            {
                insert.Parameters.AddWithValue("id", id);
                insert.Parameters.AddWithValue("user_id", user.Id);
                insert.Parameters.AddWithValue("code_hash", hash);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }

        try
        {
            await sender.SendAsync(user.Email, code, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Password reset email delivery failed for user {UserId}.", user.Id);
            await using var connection = await dataSource.OpenConnectionAsync(CancellationToken.None);
            await using var expire = new NpgsqlCommand(
                "update identity.password_reset_otp set consumed_at = now() where id = @id and user_id = @user_id and consumed_at is null",
                connection);
            expire.Parameters.AddWithValue("id", id);
            expire.Parameters.AddWithValue("user_id", user.Id);
            await expire.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    public async Task<bool> ConsumeAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        Guid id;
        string expectedHash;
        DateTime expiresAt;
        int attempts;

        await using (var select = new NpgsqlCommand("""
            select id, code_hash, expires_at, attempts
              from identity.password_reset_otp
             where user_id = @user_id and consumed_at is null
             order by created_at desc limit 1 for update
            """, connection, transaction))
        {
            select.Parameters.AddWithValue("user_id", userId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await reader.DisposeAsync();
                await transaction.CommitAsync(cancellationToken);
                return false;
            }
            id = reader.GetGuid(0);
            expectedHash = reader.GetString(1).Trim();
            expiresAt = reader.GetDateTime(2);
            attempts = reader.GetInt32(3);
        }

        if (expiresAt <= DateTime.UtcNow || attempts >= MaxAttempts)
        {
            await using var expire = new NpgsqlCommand(
                "update identity.password_reset_otp set consumed_at = now() where id = @id", connection, transaction);
            expire.Parameters.AddWithValue("id", id);
            await expire.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var suppliedHash = Hash(id, userId, code.Trim());
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedHash), Encoding.ASCII.GetBytes(suppliedHash));
        await using (var update = new NpgsqlCommand("""
            update identity.password_reset_otp
               set attempts = attempts + 1,
                   consumed_at = case when @matches or attempts + 1 >= @max_attempts then now() else consumed_at end
             where id = @id
            """, connection, transaction))
        {
            update.Parameters.AddWithValue("matches", matches);
            update.Parameters.AddWithValue("max_attempts", MaxAttempts);
            update.Parameters.AddWithValue("id", id);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return matches;
    }
}
