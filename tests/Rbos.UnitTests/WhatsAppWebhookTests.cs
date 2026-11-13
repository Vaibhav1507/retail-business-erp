using Rbos.Api;

namespace Rbos.UnitTests;

public class WhatsAppWebhookTests
{
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
