using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Rbos.Api.Application;

namespace Rbos.Api;

public sealed record PasswordResetRequest(string? Email);
public sealed record PasswordResetConfirmRequest(string? Email, string? Code, string? NewPassword);

public static class AuthPasswordResetEndpoints
{
    static string? CleanEmail(string? value)
    {
        var email = value?.Trim();
        return email is { Length: > 3 and <= 200 } && MailAddress.TryCreate(email, out _) ? email : null;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/auth/password-reset/request", async (
            PasswordResetRequest request,
            UserManager<IdentityUser<Guid>> users,
            PasswordResetOtpService reset,
            CancellationToken cancellationToken) =>
        {
            if (!reset.IsEnabled)
                return ApiResults.Fail(503, "Password recovery is not configured yet. Please contact support.");
            var email = CleanEmail(request.Email);
            if (email is null) return ApiResults.Fail(400, "Enter a valid email address.");

            // Identical response for known, unknown and rate-limited accounts.
            var user = await users.FindByEmailAsync(email);
            if (user is not null) await reset.SendAsync(user, cancellationToken);
            return Results.Ok(new { accepted = true });
        }).AllowAnonymous();

        api.MapPost("/auth/password-reset/confirm", async (
            PasswordResetConfirmRequest request,
            UserManager<IdentityUser<Guid>> users,
            PasswordResetOtpService reset,
            AuthSessionService sessions,
            CancellationToken cancellationToken) =>
        {
            if (!reset.IsEnabled)
                return ApiResults.Fail(503, "Password recovery is not configured yet. Please contact support.");
            var email = CleanEmail(request.Email);
            if (email is null || request.Code is null || request.Code.Length != 6 ||
                request.Code.Any(character => !char.IsAsciiDigit(character)) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
                return ApiResults.Fail(400, "Enter your email, six-digit code and a valid new password.");

            var user = await users.FindByEmailAsync(email);
            if (user is null || !await reset.ConsumeAsync(user.Id, request.Code, cancellationToken))
                return ApiResults.Fail(400, "The code is incorrect or has expired. Request a new code and try again.");

            var token = await users.GeneratePasswordResetTokenAsync(user);
            var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
            if (!result.Succeeded)
                return ApiResults.Fail(400, string.Join(" ", result.Errors.Select(error => error.Description)));

            await sessions.RevokeAllSessionsAsync(user.Id, "credential.password_reset", cancellationToken);
            return Results.Ok(new { reset = true });
        }).AllowAnonymous();
    }
}
