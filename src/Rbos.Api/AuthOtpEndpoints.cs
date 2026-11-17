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

    static async ValueTask<object?> RequireOtp(EndpointFilterInvocationContext c, EndpointFilterDelegate next)
        => OtpEnabled(c.HttpContext.RequestServices.GetRequiredService<IConfiguration>()) ? await next(c) : Results.NotFound();

    public static bool OtpEnabled(IConfiguration cfg) => cfg.GetValue("Otp:Enabled", true);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/auth/config", (IConfiguration cfg) => Results.Ok(new { otpEnabled = OtpEnabled(cfg) })).AllowAnonymous();

        api.MapPost("/auth/otp/send", async (SendOtpRequest m, UserManager<IdentityUser<Guid>> um, OtpService otp, CancellationToken ct) =>
        {
            var mobile = Clean(m.Mobile);
            if (mobile is null) return ApiResults.Fail(400, "Enter a 10-digit mobile number.");
            if (m.Purpose == "signup")
            {
                if (await um.Users.AnyAsync(u => u.PhoneNumber == mobile, ct)) return ApiResults.Fail(400, "This mobile number is already registered. Please sign in.");
            }
            else if (m.Purpose == "login")
            {
                // Same response whether or not the number exists, to avoid revealing which mobiles are registered.
                if (!await um.Users.AnyAsync(u => u.PhoneNumber == mobile && u.PhoneNumberConfirmed, ct)) return Results.Ok(new { sent = true });
            }
            else return ApiResults.Fail(400, "Invalid request.");
            var r = await otp.SendAsync(mobile, m.Purpose!, ct);
            return r.Ok ? Results.Ok(new { sent = true, devCode = r.DevCode }) : ApiResults.Fail(400, r.Error);
        }).AllowAnonymous().AddEndpointFilter(RequireOtp);

        api.MapPost("/auth/otp/verify", async (VerifyOtpRequest m, OtpService otp, CancellationToken ct) =>
        {
            var mobile = Clean(m.Mobile);
            if (mobile is null || string.IsNullOrWhiteSpace(m.Code)) return ApiResults.Fail(400, "Enter the mobile number and code.");
            var proof = await otp.VerifyAsync(mobile, "signup", m.Code, ct);
            return proof is null ? ApiResults.Fail(400, "Wrong or expired code.") : Results.Ok(new { proof });
        }).AllowAnonymous().AddEndpointFilter(RequireOtp);

        api.MapPost("/auth/register", async (RegisterRequest m, UserManager<IdentityUser<Guid>> um, OtpService otp, IConfiguration cfg, CancellationToken ct) =>
        {
            if (!cfg.GetValue<bool>("Rbos:AllowOpenRegistration") && await um.Users.AnyAsync(ct))
                return ApiResults.Fail(403, "Registration is closed.");
            if (string.IsNullOrWhiteSpace(m.Email) || m.Email.Length > 200) return ApiResults.Fail(400, "Invalid email.");
            var mobile = Clean(m.Mobile);
            if (mobile is null) return ApiResults.Fail(400, "Enter a 10-digit mobile number.");
            var otpOn = OtpEnabled(cfg);
            if (otpOn && (m.OtpProof is null || !await otp.ProofValidAsync(m.OtpProof.Value, mobile, ct))) return ApiResults.Fail(400, "Verify your mobile number first.");
            var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = m.Email.Trim(), Email = m.Email.Trim(), PhoneNumber = mobile, PhoneNumberConfirmed = otpOn };
            var r = await um.CreateAsync(user, m.Password ?? "");
            if (!r.Succeeded)
            {
                if (r.Errors.Any(e => e.Code.StartsWith("Duplicate"))) return ApiResults.Fail(400, "Registration could not be completed.");
                return ApiResults.Fail(400, string.Join(" ", r.Errors.Select(e => e.Description)));
            }
            if (otpOn) await otp.ConsumeProofAsync(m.OtpProof!.Value, mobile, ct);
            return Results.Ok();
        }).AllowAnonymous();

        api.MapPost("/auth/login/otp", async (OtpLoginRequest m, UserManager<IdentityUser<Guid>> um, SignInManager<IdentityUser<Guid>> sm, OtpService otp, AuthSessionService sessions, HttpContext context, CancellationToken ct) =>
        {
            var bad = ApiResults.Fail(401, "Wrong or expired code.");
            var mobile = Clean(m.Mobile);
            if (mobile is null || string.IsNullOrWhiteSpace(m.Code)) return bad;
            var u = await um.Users.FirstOrDefaultAsync(x => x.PhoneNumber == mobile && x.PhoneNumberConfirmed, ct);
            if (u is null || await um.IsLockedOutAsync(u)) return bad;
            if (await otp.VerifyAsync(mobile, "login", m.Code, ct) is null) return bad;
            await sessions.SignInAsync(context, u, sm, ct);
            return Results.Ok();
        }).AllowAnonymous().AddEndpointFilter(RequireOtp);

        api.MapPost("/profile/mobile/send-otp", async (ChangeMobileRequest m, ClaimsPrincipal p, UserManager<IdentityUser<Guid>> um, OtpService otp, CancellationToken ct) =>
        {
            var mobile = Clean(m.Mobile);
            if (mobile is null) return ApiResults.Fail(400, "Enter a 10-digit mobile number.");
            var uid = Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (await um.Users.AnyAsync(u => u.PhoneNumber == mobile && u.Id != uid, ct)) return ApiResults.Fail(400, "This mobile number is already used by another account.");
            var r = await otp.SendAsync(mobile, "change", ct);
            return r.Ok ? Results.Ok(new { sent = true, devCode = r.DevCode }) : ApiResults.Fail(400, r.Error);
        }).RequireAuthorization().AddEndpointFilter(RequireOtp);

        api.MapPost("/profile/mobile/verify", async (ChangeMobileVerify m, ClaimsPrincipal p, UserManager<IdentityUser<Guid>> um, OtpService otp, TenantDb db, CancellationToken ct) =>
        {
            var mobile = Clean(m.Mobile);
            if (mobile is null || string.IsNullOrWhiteSpace(m.Code)) return ApiResults.Fail(400, "Enter the mobile number and code.");
            var uid = Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await um.FindByIdAsync(uid.ToString());
            if (user is null) return Results.Unauthorized();
            if (await um.Users.AnyAsync(u => u.PhoneNumber == mobile && u.Id != uid, ct)) return ApiResults.Fail(400, "This mobile number is already used by another account.");
            if (await otp.VerifyAsync(mobile, "change", m.Code, ct) is null) return ApiResults.Fail(400, "Wrong or expired code.");
            user.PhoneNumber = mobile; user.PhoneNumberConfirmed = true;
            var r = await um.UpdateAsync(user);
            if (!r.Succeeded) return ApiResults.Fail(400, "Could not save the mobile number.");
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
        }).RequireAuthorization().AddEndpointFilter(RequireOtp);
    }
}
