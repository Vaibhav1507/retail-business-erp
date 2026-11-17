using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Rbos.Api;

public sealed record PasskeyCredentialRequest(string CredentialJson, string? Name = null);
public sealed record PasskeyRegistrationStartRequest(string Password);
public sealed record PasskeyRemovalRequest(string Password);

public static class AuthPasskeyEndpoints
{
    public static bool IsEnabled(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Auth:Passkeys:ServerDomain"]);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/auth/passkeys/config", (IConfiguration configuration) =>
            Results.Ok(new { enabled = IsEnabled(configuration) })).AllowAnonymous();

        api.MapPost("/auth/passkeys/creation-options", async (
            PasskeyRegistrationStartRequest request,
            ClaimsPrincipal principal,
            UserManager<IdentityUser<Guid>> users,
            SignInManager<IdentityUser<Guid>> signIn,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!IsEnabled(configuration)) return ApiResults.Fail(503, "Passkeys are not configured for this deployment.");
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Password)) return Results.Unauthorized();
            var passwordCheck = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!passwordCheck.Succeeded) return Results.Unauthorized();
            var userId = await users.GetUserIdAsync(user);
            var name = user.UserName ?? user.Email ?? "RBOS user";
            var options = await signIn.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
            {
                Id = userId,
                Name = name,
                DisplayName = name
            });
            return Results.Content(options, "application/json");
        }).RequireAuthorization();

        api.MapPost("/auth/passkeys/register", async (
            PasskeyCredentialRequest request,
            ClaimsPrincipal principal,
            UserManager<IdentityUser<Guid>> users,
            SignInManager<IdentityUser<Guid>> signIn,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!IsEnabled(configuration)) return ApiResults.Fail(503, "Passkeys are not configured for this deployment.");
            if (string.IsNullOrWhiteSpace(request.CredentialJson) || request.CredentialJson.Length > 64_000)
                return ApiResults.Fail(400, "Invalid passkey credential payload.");
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            if (request.Name?.Trim().Length > 64) return ApiResults.Fail(400, "Passkey name must be 64 characters or fewer.");

            PasskeyAttestationResult attestation;
            try { attestation = await signIn.PerformPasskeyAttestationAsync(request.CredentialJson); }
            catch (InvalidOperationException) { return ApiResults.Fail(400, "Passkey registration state is missing or expired. Start again."); }

            if (!attestation.Succeeded ||
                !string.Equals(await users.GetUserIdAsync(user), attestation.UserEntity.Id, StringComparison.Ordinal))
                return ApiResults.Fail(400, "The passkey could not be validated for this account.");

            attestation.Passkey.Name = string.IsNullOrWhiteSpace(request.Name) ? "This device" : request.Name.Trim();
            var saved = await users.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
            return saved.Succeeded
                ? Results.Ok(new { registered = true })
                : ApiResults.Fail(400, "The passkey could not be saved.");
        }).RequireAuthorization();

        api.MapGet("/auth/passkeys", async (
            ClaimsPrincipal principal,
            UserManager<IdentityUser<Guid>> users,
            IConfiguration configuration) =>
        {
            if (!IsEnabled(configuration)) return ApiResults.Fail(503, "Passkeys are not configured for this deployment.");
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var passkeys = await users.GetPasskeysAsync(user);
            return Results.Ok(passkeys.Select(x => new
            {
                credentialId = Encode(x.CredentialId),
                name = x.Name,
                createdAt = x.CreatedAt,
                isBackedUp = x.IsBackedUp,
                isBackupEligible = x.IsBackupEligible,
                transports = x.Transports
            }));
        }).RequireAuthorization();

        api.MapDelete("/auth/passkeys/{credentialId}", async (
            string credentialId,
            [FromBody] PasskeyRemovalRequest request,
            ClaimsPrincipal principal,
            UserManager<IdentityUser<Guid>> users,
            SignInManager<IdentityUser<Guid>> signIn,
            IConfiguration configuration) =>
        {
            if (!IsEnabled(configuration)) return ApiResults.Fail(503, "Passkeys are not configured for this deployment.");
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Password)) return Results.Unauthorized();
            var passwordCheck = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!passwordCheck.Succeeded) return Results.Unauthorized();
            byte[] id;
            try { id = Decode(credentialId); }
            catch (FormatException) { return ApiResults.Fail(400, "Invalid passkey identifier."); }
            var removed = await users.RemovePasskeyAsync(user, id);
            return removed.Succeeded ? Results.Ok(new { removed = true }) : Results.NotFound();
        }).RequireAuthorization();

        api.MapPost("/auth/passkeys/request-options", async (
            SignInManager<IdentityUser<Guid>> signIn,
            IConfiguration configuration) =>
        {
            if (!IsEnabled(configuration)) return ApiResults.Fail(503, "Passkeys are not configured for this deployment.");
            var options = await signIn.MakePasskeyRequestOptionsAsync(user: null);
            return Results.Content(options, "application/json");
        }).AllowAnonymous();

        api.MapPost("/auth/passkeys/sign-in", async (
            PasskeyCredentialRequest request,
            SignInManager<IdentityUser<Guid>> signIn,
            UserManager<IdentityUser<Guid>> users,
            AuthSessionService sessions,
            HttpContext context,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!IsEnabled(configuration)) return ApiResults.Fail(503, "Passkeys are not configured for this deployment.");
            if (string.IsNullOrWhiteSpace(request.CredentialJson) || request.CredentialJson.Length > 64_000)
                return ApiResults.Fail(400, "Invalid passkey credential payload.");

            PasskeyAssertionResult<IdentityUser<Guid>> assertion;
            try { assertion = await signIn.PerformPasskeyAssertionAsync(request.CredentialJson); }
            catch (InvalidOperationException) { return ApiResults.Fail(400, "Passkey sign-in state is missing or expired. Start again."); }

            if (!assertion.Succeeded || assertion.User is null) return Results.Unauthorized();
            if (!await signIn.CanSignInAsync(assertion.User) || await users.IsLockedOutAsync(assertion.User))
                return Results.Unauthorized();

            var updated = await users.AddOrUpdatePasskeyAsync(assertion.User, assertion.Passkey);
            if (!updated.Succeeded) return Results.Unauthorized();

            await sessions.SignInAsync(context, assertion.User, signIn, ct);
            return Results.Ok(new { signedIn = true });
        }).AllowAnonymous();
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
