using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rbos.Api;

namespace Rbos.UnitTests;

public class PasskeyHostConfigurationTests
{
    private static (IConfiguration Configuration, IServiceCollection Services) Create(
        string? allowedHosts,
        string? passkeyDomain)
    {
        var values = new Dictionary<string, string?>();
        if (allowedHosts is not null) values["AllowedHosts"] = allowedHosts;
        if (passkeyDomain is not null) values["Auth:Passkeys:ServerDomain"] = passkeyDomain;
        return (new ConfigurationBuilder().AddInMemoryCollection(values).Build(), new ServiceCollection());
    }

    [Fact]
    public void Allows_custom_domain_and_explicit_run_app_fallback_without_changing_rp_domain()
    {
        var (configuration, services) = Create(
            "DhandhaMitra.in;rbos-pilot-qgmykg5ewa-as.a.run.app",
            "dhandhamitra.in");

        var domain = PasskeyHostConfiguration.Configure(configuration, services);

        Assert.Equal("dhandhamitra.in", domain);
        Assert.Equal("dhandhamitra.in;rbos-pilot-qgmykg5ewa-as.a.run.app", configuration["AllowedHosts"]);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IdentityPasskeyOptions>>().Value;
        Assert.Equal("dhandhamitra.in", options.ServerDomain);
        Assert.Equal("required", options.UserVerificationRequirement);
        Assert.Equal("required", options.ResidentKeyRequirement);
    }

    [Fact]
    public void Narrows_wildcard_default_to_the_exact_passkey_domain()
    {
        var (configuration, services) = Create("*", "dhandhamitra.in");

        PasskeyHostConfiguration.Configure(configuration, services);

        Assert.Equal("dhandhamitra.in", configuration["AllowedHosts"]);
    }

    [Fact]
    public void Keeps_passkeys_disabled_when_no_rp_domain_is_configured()
    {
        var (configuration, services) = Create("*", null);

        Assert.Null(PasskeyHostConfiguration.Configure(configuration, services));
        Assert.Equal("*", configuration["AllowedHosts"]);
        Assert.Empty(services);
    }

    [Theory]
    [InlineData("dhandhamitra.in;*", "dhandhamitra.in")]
    [InlineData("*.dhandhamitra.in", "dhandhamitra.in")]
    [InlineData("rbos-pilot-qgmykg5ewa-as.a.run.app", "dhandhamitra.in")]
    [InlineData("dhandhamitra.in;bad host", "dhandhamitra.in")]
    public void Rejects_wildcards_invalid_hosts_or_allowlists_missing_the_rp_domain(
        string allowedHosts,
        string passkeyDomain)
    {
        var (configuration, services) = Create(allowedHosts, passkeyDomain);

        Assert.Throws<InvalidOperationException>(() =>
            PasskeyHostConfiguration.Configure(configuration, services));
    }

    [Theory]
    [InlineData("not a host")]
    [InlineData("https://dhandhamitra.in")]
    [InlineData("*")]
    public void Rejects_invalid_passkey_rp_domains(string passkeyDomain)
    {
        var (configuration, services) = Create("dhandhamitra.in", passkeyDomain);

        Assert.Throws<InvalidOperationException>(() =>
            PasskeyHostConfiguration.Configure(configuration, services));
    }

    [Fact]
    public void Passkey_capability_is_only_exposed_on_the_configured_rp_hostname()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Passkeys:ServerDomain"] = "dhandhamitra.in"
            })
            .Build();

        var canonical = new DefaultHttpContext();
        canonical.Request.Host = new HostString("dhandhamitra.in");
        var fallback = new DefaultHttpContext();
        fallback.Request.Host = new HostString("rbos-pilot-qgmykg5ewa-as.a.run.app");

        Assert.True(AuthPasskeyEndpoints.IsEnabled(configuration, canonical));
        Assert.False(AuthPasskeyEndpoints.IsEnabled(configuration, fallback));
    }
}
