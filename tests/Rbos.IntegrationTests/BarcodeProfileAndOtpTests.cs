using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public class BarcodeTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    const string Ean13 = "4006381333931";
    const string UpcA = "036000291452";

    static async Task<Client> NewClient(ApiFactory f) { var c = new Client(f); await c.SignUpAndBootstrap(); return c; }

    [Fact] public async Task Barcode_can_be_added_looked_up_and_removed()
    {
        var c = await NewClient(f); var p = await c.AddProduct("Cable");
        var add = await c.Send(HttpMethod.Post, $"/products/{p}/barcodes", new { code = Ean13, symbology = "ean_13", packQuantity = 2 });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var id = (await add.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var found = await c.GetJson($"/products/by-barcode?code={Ean13}");
        Assert.Equal(p, found.GetProperty("productId").GetGuid());
        Assert.Equal(2, found.GetProperty("packQuantity").GetInt32());

        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Delete, $"/products/{p}/barcodes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Raw($"/products/by-barcode?code={Ean13}")).StatusCode);
    }

    [Fact] public async Task Invalid_check_digit_and_bad_codes_are_rejected()
    {
        var c = await NewClient(f); var p = await c.AddProduct("Cable");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, $"/products/{p}/barcodes", new { code = "4006381333932" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, $"/products/{p}/barcodes", new { code = "AB" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, $"/products/{p}/barcodes", new { code = "has space" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, $"/products/{p}/barcodes", new { code = Ean13, packQuantity = 0 })).StatusCode);
    }

    [Fact] public async Task Duplicate_barcode_is_rejected_and_upca_matches_its_ean13_form()
    {
        var c = await NewClient(f); var p = await c.AddProduct("Cable"); var q = await c.AddProduct("Charger");
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Post, $"/products/{p}/barcodes", new { code = UpcA })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Send(HttpMethod.Post, $"/products/{q}/barcodes", new { code = "0" + UpcA })).StatusCode);
        var found = await c.GetJson($"/products/by-barcode?code={UpcA}");
        Assert.Equal(p, found.GetProperty("productId").GetGuid());
    }

    [Fact] public async Task Barcodes_are_isolated_between_businesses()
    {
        var a = await NewClient(f); var b = await NewClient(f);
        var p = await a.AddProduct("Cable");
        Assert.Equal(HttpStatusCode.OK, (await a.Send(HttpMethod.Post, $"/products/{p}/barcodes", new { code = Ean13 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Raw($"/products/by-barcode?code={Ean13}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Send(HttpMethod.Post, $"/products/{p}/barcodes", new { code = Ean13 })).StatusCode);
    }

    [Fact] public async Task Serial_tracking_toggles_on_an_empty_product_and_unknown_product_is_404()
    {
        var c = await NewClient(f); var p = await c.AddProduct("Phone");
        var on = await c.Send(HttpMethod.Put, $"/products/{p}/serial-tracking", new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.True((await c.GetJson($"/products/{p}/barcodes")).GetProperty("trackSerials").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, $"/products/{p}/serial-tracking", new { enabled = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.Send(HttpMethod.Put, $"/products/{Guid.NewGuid()}/serial-tracking", new { enabled = true })).StatusCode);
    }
}

public class BusinessProfileTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static object Profile(string name = "Synthetic Traders", string pin = "560001", string gstStatus = "NotRegistered", string? gstin = null)
        => new { businessName = name, businessCategory = "general_retail", ownerName = "Owner", address = "1 Test Road", city = "Bengaluru", state = "Karnataka", pinCode = pin, gstStatus, gstin, financialYearStartMonth = 4, language = "en" };

    static async Task<Client> NewClient(ApiFactory f) { var c = new Client(f); await c.SignUpAndBootstrap(); return c; }

    [Fact] public async Task Invalid_profiles_are_rejected()
    {
        var c = await NewClient(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, "/business-profile", Profile(pin: "012345"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, "/business-profile", Profile(gstStatus: "Regular"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, "/business-profile", Profile(gstStatus: "Regular", gstin: "BADGSTIN"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Put, "/business-profile", Profile(gstStatus: "Unknown"))).StatusCode);
    }

    [Fact] public async Task Valid_regular_gst_profile_saves_and_completes_onboarding()
    {
        var c = await NewClient(f);
        var save = await c.Send(HttpMethod.Put, "/business-profile", Profile(gstStatus: "Regular", gstin: "27aapfu0939f1zv"));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var got = await c.GetJson("/business-profile");
        Assert.True(got.GetProperty("onboardingCompleted").GetBoolean());
        Assert.Equal("Regular", got.GetProperty("gstStatus").GetString());
        Assert.Equal("27AAPFU0939F1ZV", got.GetProperty("gstin").GetString());
    }

    [Fact] public async Task Business_name_is_fixed_after_onboarding_and_gstin_clears_when_not_registered()
    {
        var c = await NewClient(f);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, "/business-profile", Profile(name: "First Name", gstStatus: "Regular", gstin: "27AAPFU0939F1ZV"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Send(HttpMethod.Put, "/business-profile", Profile(name: "Changed Name", gstStatus: "NotRegistered"))).StatusCode);
        var got = await c.GetJson("/business-profile");
        Assert.Equal("First Name", got.GetProperty("businessName").GetString());
        Assert.Equal(JsonValueKind.Null, got.GetProperty("gstin").ValueKind);
    }
}

public class OtpFailureTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    static string NewMobile() => "9" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    [Fact] public async Task Invalid_mobile_and_purpose_are_rejected()
    {
        var c = new Client(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/auth/otp/send", new { mobile = "123", purpose = "signup" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/auth/otp/send", new { mobile = NewMobile(), purpose = "other" })).StatusCode);
    }

    [Fact] public async Task Wrong_code_is_rejected_and_attempts_are_limited()
    {
        var c = new Client(f); var mobile = NewMobile();
        var sent = await c.Send(HttpMethod.Post, "/auth/otp/send", new { mobile, purpose = "signup" });
        var code = (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("devCode").GetString()!;
        var wrong = code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/auth/otp/verify", new { mobile, code = wrong })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Send(HttpMethod.Post, "/auth/otp/verify", new { mobile, code })).StatusCode);
    }

    [Fact] public async Task Register_without_a_valid_proof_is_rejected()
    {
        var c = new Client(f);
        var r = await c.Send(HttpMethod.Post, "/auth/register", new { email = c.Email, password = Client.Password, mobile = NewMobile(), otpProof = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact] public async Task Proof_cannot_be_reused_for_a_second_registration()
    {
        var c = new Client(f); await c.Register();
        var other = new Client(f);
        var mobile = NewMobile();
        var sent = await other.Send(HttpMethod.Post, "/auth/otp/send", new { mobile, purpose = "signup" });
        var code = (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("devCode").GetString();
        var ver = await other.Send(HttpMethod.Post, "/auth/otp/verify", new { mobile, code });
        var proof = (await ver.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("proof").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await other.Send(HttpMethod.Post, "/auth/register", new { email = other.Email, password = Client.Password, mobile, otpProof = proof })).StatusCode);
        var again = await other.Send(HttpMethod.Post, "/auth/register", new { email = "second-" + other.Email, password = Client.Password, mobile, otpProof = proof });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }
}
