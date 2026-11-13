using Rbos.Api;

namespace Rbos.UnitTests;

public class WhatsAppWebhookTests
{
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
