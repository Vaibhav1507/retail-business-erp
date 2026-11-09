using System.Security.Claims;
using System.Text.RegularExpressions;
using Rbos.Api.Persistence;

namespace Rbos.Api;

public sealed record ProfileRequest(string? BusinessName, string? BusinessCategory, string? OwnerName, string? Mobile, string? Address, string? City, string? State,
    string? PinCode, string? GstStatus, string? Gstin, int FinancialYearStartMonth, string? Language);

public static class ProfileEndpoints
{
    static readonly string[] Categories = ["electronics_mobile", "electrical_hardware", "general_retail", "garments_apparel", "automobile_spares", "sports_fitness", "pharmacy_medical", "other_retail"];
    static readonly string[] GstStatuses = ["NotRegistered", "Regular", "Composition", "Undecided"];

    static string? Validate(ProfileRequest m)
    {
        static bool Bad(string? v, int max) => string.IsNullOrWhiteSpace(v) || v.Trim().Length > max;
        if (Bad(m.BusinessName, 120)) return "Business name is required.";
        if (m.BusinessCategory is null || !Categories.Contains(m.BusinessCategory)) return "Choose a business category.";
        if (Bad(m.OwnerName, 120)) return "Owner or contact name is required.";
        if (Bad(m.Address, 250)) return "Business address is required.";
        if (Bad(m.City, 80)) return "City or town is required.";
        if (Bad(m.State, 80)) return "State is required.";
        if (m.PinCode is null || !Regex.IsMatch(m.PinCode.Trim(), "^[1-9][0-9]{5}$")) return "Enter a 6-digit PIN code.";
        if (m.GstStatus is null || !GstStatuses.Contains(m.GstStatus)) return "Choose a GST registration status.";
        var needsGstin = m.GstStatus is "Regular" or "Composition";
        if (needsGstin && (m.Gstin is null || !Regex.IsMatch(m.Gstin.Trim().ToUpperInvariant(), "^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][A-Z0-9]Z[A-Z0-9]$"))) return "Enter a valid 15-character GSTIN.";
        if (m.FinancialYearStartMonth is < 1 or > 12) return "Choose the financial-year start month.";
        if (m.Language != "en") return "Language not supported yet.";
        return null;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/business-profile", async (ClaimsPrincipal p, TenantDb db, CancellationToken ct) =>
        {
            var uid = Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tid = await db.FindTenantAsync(uid, ct);
            if (tid is null) return Results.Ok(new { exists = false, onboardingCompleted = false });
            await using var s = await db.BeginAsync(uid, tid, ct);
            await using var c = s.Command("select display_name,business_category,owner_name,mobile,mobile_verification_status,address,city,state,pin_code,gst_status,gstin,financial_year_start_month,language,onboarding_completed from core.business_profile");
            await using var r = await c.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return Results.Ok(new { exists = false, onboardingCompleted = false });
            string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
            return Results.Ok(new
            {
                exists = true, businessName = S(0), businessCategory = S(1), ownerName = S(2), mobile = S(3), mobileVerification = S(4), address = S(5), city = S(6), state = S(7),
                pinCode = S(8), gstStatus = S(9), gstin = S(10), financialYearStartMonth = (int)r.GetInt16(11), language = S(12), currency = "INR", onboardingCompleted = r.GetBoolean(13),
            });
        }).RequireAuthorization();

        api.MapPut("/business-profile", async (ProfileRequest m, ClaimsPrincipal p, TenantDb db, Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser<Guid>> um, CancellationToken ct) =>
        {
            var err = Validate(m);
            if (err is not null) return ApiResults.Fail(400, err);
            var uid = Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var tid = await db.FindTenantAsync(uid, ct);
            if (tid is null) return ApiResults.Fail(409, "No business set up.");
            var user = await um.FindByIdAsync(uid.ToString());
            var verified = user is { PhoneNumberConfirmed: true, PhoneNumber: not null } ? user.PhoneNumber : null;
            await using var s = await db.BeginAsync(uid, tid, ct);
            // Business name, category and owner are fixed once set up; the mobile only changes through OTP verification.
            await using var c = s.Command(@"update core.business_profile set
                display_name=case when onboarding_completed then display_name else @n end,
                business_category=case when onboarding_completed then business_category else @cat end,
                owner_name=case when onboarding_completed then owner_name else @o end,
                mobile=coalesce(@m, mobile), mobile_verification_status=case when @m is not null then 'Verified' else mobile_verification_status end, address=@a,
                gst_status=@g, gstin=@gi, financial_year_start_month=@fy, language=@l, onboarding_completed=true");
            var needsGstin = m.GstStatus is "Regular" or "Composition";
            c.Parameters.AddWithValue("n", m.BusinessName!.Trim());
            c.Parameters.AddWithValue("cat", m.BusinessCategory!);
            c.Parameters.AddWithValue("o", m.OwnerName!.Trim());
            c.Parameters.AddWithValue("m", (object?)verified ?? DBNull.Value);
            c.Parameters.AddWithValue("a", m.Address!.Trim());
            c.Parameters.AddWithValue("c", m.City!.Trim());
            c.Parameters.AddWithValue("s", m.State!.Trim());
            c.Parameters.AddWithValue("p", m.PinCode!.Trim());
            c.Parameters.AddWithValue("g", m.GstStatus!);
            c.Parameters.AddWithValue("gi", needsGstin ? m.Gstin!.Trim().ToUpperInvariant() : DBNull.Value);
            c.Parameters.AddWithValue("fy", (short)m.FinancialYearStartMonth);
            c.Parameters.AddWithValue("l", m.Language!);
            var n = await c.ExecuteNonQueryAsync(ct);
            if (n == 0) return ApiResults.Fail(409, "No business profile found.");
            await s.CommitAsync(ct);
            return Results.Ok();
        }).RequireAuthorization();
    }
}
