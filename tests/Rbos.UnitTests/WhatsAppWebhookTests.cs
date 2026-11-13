using System.Security.Cryptography;
using System.Text;
using Rbos.Api;

namespace Rbos.UnitTests;

public class WhatsAppWebhookTests
{
    [Fact]
    public void Verifies_Meta_HMAC_signature_and_rejects_tampering()
    {
        const string secret = "test-app-secret";
        var body = Encoding.UTF8.GetBytes("{\"entry\":[]}");
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        var signature = "sha256=" + Convert.ToHexString(digest).ToLowerInvariant();

        Assert.True(WhatsAppWebhookEndpoints.VerifySignature(secret, signature, body));
        Assert.False(WhatsAppWebhookEndpoints.VerifySignature("wrong-secret", signature, body));
        Assert.False(WhatsAppWebhookEndpoints.VerifySignature(secret, "sha256=not-hex", body));
        Assert.False(WhatsAppWebhookEndpoints.VerifySignature(secret, "sha1=" + Convert.ToHexString(digest), body));
    }

    [Theory]
    [InlineData("9876543210", true)]
    [InlineData("+91 9876543210", true)]
    [InlineData("1234567890", false)]
    [InlineData("09876543210", false)]
    [InlineData("987654321", false)]
    [InlineData("+1 9876543210", false)]
    public void Accepts_only_valid_Indian_mobile_numbers(string number, bool expected)
    {
        Assert.Equal(expected, WhatsAppWebhookEndpoints.IsIndianMobileNumber(number));
    }

    [Theory]
    [InlineData("STOP", true)]
    [InlineData(" stop ", true)]
    [InlineData("Unsubscribe", true)]
    [InlineData("CANCEL", true)]
    [InlineData("END", true)]
    [InlineData("QUIT", true)]
    [InlineData("STOP ALL", true)]
    [InlineData("", false)]
    [InlineData("Please stop the order", false)]
    [InlineData("Don't stop", false)]
    [InlineData(null, false)]
    public void Recognizes_explicit_opt_out_keywords(string? body, bool expected)
    {
        Assert.Equal(expected, WhatsAppWebhookEndpoints.IsOptOutKeyword(body));
    }
}
