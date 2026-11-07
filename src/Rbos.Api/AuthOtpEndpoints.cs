using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rbos.Api.Application;
using Rbos.Api.Persistence;

namespace Rbos.Api;

public sealed record SendOtpRequest(string? Mobile, string? Purpose);
public sealed record VerifyOtpRequest(string? Mobile, string? Code);
public sealed record RegisterRequest(string? Email, string? Password, string? Mobile, Guid? OtpProof);
public sealed record OtpLoginRequest(string? Mobile, string? Code);
public sealed record ChangeMobileRequest(string? Mobile);
public sealed record ChangeMobileVerify(string? Mobile, string? Code);

public static class AuthOtpEndpoints
{
    static string? Clean(string? m) => m is not null && OtpService.MobileRe.IsMatch(m.Trim()) ? m.Trim() : null;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/auth/otp/send", async (SendOtpRequest m, UserManager<IdentityUser<Guid>> um, OtpService otp, CancellationToken ct) =>
        {
            var mobile = Clean(m.Mobile);
            if (mobile is null) return Results.BadRequest(new { error = "Enter a 10-digit mobile number." });
            if (m.Purpose == "signup")
            {
                if (await um.Users.AnyAsync(u => u.PhoneNumber == mobile, ct)) return Results.BadRequest(new { error = "This mobile number is already registered. Please sign in." });
            }
            else if (m.Purpose == "login")
            {
                // Same response whether or not the number exists, to avoid revealing which mobiles are registered.
                if (!await um.Users.AnyAsync(u => u.PhoneNumber == mobile && u.PhoneNumberConfirmed, ct)) return Results.Ok(new { sent = true });
            }
            else return Results.BadRequest(new { error = "Invalid request." });
            var r = await otp.SendAsync(mobile, m.Purpose!, ct);
            return r.Ok ? Results.Ok(new { sent = true, devCode = r.DevCode }) : Results.BadRequest(new { error = r.Error });
        }).AllowAnonymous();

        api.MapPost("/auth/otp/verify", async (VerifyOtpRequest m, OtpService otp, CancellationToken ct) =>
        {
            var mobile = Clean(m.Mobile);
            if (mobile is null || string.IsNullOrWhiteSpace(m.Code)) return Results.BadRequest(new { error = "Enter the mobile number and code." });
            var proof = await otp.VerifyAsync(mobile, "signup", m.Code, ct);
            return proof is null ? Results.BadRequest(new { error = "Wrong or expired code." }) : Results.Ok(new { proof });
        }).AllowAnonymous();

        api.MapPost("/auth/register", async (RegisterRequest m, UserManager<IdentityUser<Guid>> um, OtpService otp, IConfiguration cfg, CancellationToken ct) =>
        {
            if (!cfg.GetValue<bool>("Rbos:AllowOpenRegistration") && await um.Users.AnyAsync(ct))
                return Results.Json(new { error = "Registration is closed." }, statusCode: 403);
            if (string.IsNullOrWhiteSpace(m.Email) || m.Email.Length > 200) return Results.BadRequest(new { error = "Invalid email." });
            var mobile = Clean(m.Mobile);
            if (mobile is null) return Results.BadRequest(new { error = "Enter a 10-digit mobile number." });
            if (m.OtpProof is null || !await otp.ProofValidAsync(m.OtpProof.Value, mobile, ct)) return Results.BadRequest(new { error = "Verify your mobile number first." });
            var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = m.Email.Trim(), Email = m.Email.Trim(), PhoneNumber = mobile, PhoneNumberConfirmed = true };
            var r = await um.CreateAsync(user, m.Password ?? "");
            if (!r.Succeeded)
            {
                if (r.Errors.Any(e => e.Code.StartsWith("Duplicate"))) return Results.BadRequest(new { error = "Registration could not be completed." });
                return Results.BadRequest(new { error = string.Join(" ", r.Errors.Select(e => e.Description)) });
            }
            await otp.ConsumeProofAsync(m.OtpProof.Value, mobile, ct);
            return Results.Ok();
        }).AllowAnonymous();

        api.MapPost("/auth/login/otp", async (OtpLoginRequest m, UserManager<IdentityUser<Guid>> um, SignInManager<IdentityUser<Guid>> sm, OtpService otp, CancellationToken ct) =>
        {
            var bad = Results.Json(new { error = "Wrong or expired code." }, statusCode: 401);
            var mobile = Clean(m.Mobile);
            if (mobile is null || string.IsNullOrWhiteSpace(m.Code)) return bad;
            var u = await um.Users.FirstOrDefaultAsync(x => x.PhoneNumber == mobile && x.PhoneNumberConfirmed, ct);
            if (u is null || await um.IsLockedOutAsync(u)) return bad;
            if (await otp.VerifyAsync(mobile, "login", m.Code, ct) is null) return bad;
            await sm.SignInAsync(u, isPersistent: false);
            return Results.Ok();
        }).AllowAnonymous();

        api.MapPost("/profile/mobile/send-otp", async (ChangeMobileRequest m, ClaimsPrincipal p, UserManager<IdentityUser<Guid>> um, OtpService otp, CancellationToken ct) =>
        {
            var mobile = Clean(m.Mobile);
            if (mobile is null) return Results.BadRequest(new { error = "Enter a 10-digit mobile number." });
            var uid = Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (await um.Users.AnyAsync(u => u.PhoneNumber == mobile && u.Id != uid, ct)) return Results.BadRequest(new { error = "This mobile number is already used by another account." });
            var r = await otp.SendAsync(mobile, "change", ct);
            return r.Ok ? Results.Ok(new { sent = true, devCode = r.DevCode }) : Results.BadRequest(new { error = r.Error });
        }).RequireAuthorization();

        api.MapPost("/profile/mobile/verify", async (ChangeMobileVerify m, ClaimsPrincipal p, UserManager<IdentityUser<Guid>> um, OtpService otp, TenantDb db, CancellationToken ct) =>
        {
            var mobile = Clean(m.Mobile);
            if (mobile is null || string.IsNullOrWhiteSpace(m.Code)) return Results.BadRequest(new { error = "Enter the mobile number and code." });
            var uid = Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await um.FindByIdAsync(uid.ToString());
            if (user is null) return Results.Unauthorized();
            if (await um.Users.AnyAsync(u => u.PhoneNumber == mobile && u.Id != uid, ct)) return Results.BadRequest(new { error = "This mobile number is already used by another account." });
            if (await otp.VerifyAsync(mobile, "change", m.Code, ct) is null) return Results.BadRequest(new { error = "Wrong or expired code." });
            user.PhoneNumber = mobile; user.PhoneNumberConfirmed = true;
            var r = await um.UpdateAsync(user);
            if (!r.Succeeded) return Results.BadRequest(new { error = "Could not save the mobile number." });
            var tid = await db.FindTenantAsync(uid, ct);
            if (tid is not null)
            {
                await using var s = await db.BeginAsync(uid, tid, ct);
                await using var c = s.Command("update core.business_profile set mobile=@m, mobile_verification_status='Verified'");
                c.Parameters.AddWithValue("m", mobile);
                await c.ExecuteNonQueryAsync(ct);
                await s.CommitAsync(ct);
            }
            return Results.Ok(new { mobile });
        }).RequireAuthorization();
    }
}
