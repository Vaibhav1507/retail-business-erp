using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Rbos.Api;

/// <summary>
/// Validates the HTTP host allowlist independently from the single WebAuthn
/// relying-party domain. Multiple explicit HTTP hosts are safe; WebAuthn still
/// remains bound to exactly one canonical hostname.
/// </summary>
public static class PasskeyHostConfiguration
{
    public static string? Configure(IConfiguration configuration, IServiceCollection services)
    {
        var passkeyDomain = configuration["Auth:Passkeys:ServerDomain"]?
            .Trim()
            .TrimEnd('.')
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(passkeyDomain))
            return null;

        ValidateDnsHost(passkeyDomain, "Auth:Passkeys:ServerDomain");

        var configuredHosts = configuration["AllowedHosts"]?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        if (configuredHosts.Length == 0 ||
            (configuredHosts.Length == 1 && configuredHosts[0] == "*"))
        {
            // Preserve the existing secure default: when no explicit allowlist
            // exists, narrow it to the WebAuthn RP domain rather than a wildcard.
            configuredHosts = [passkeyDomain];
        }
        else
        {
            var normalizedHosts = new List<string>(configuredHosts.Length);
            foreach (var configuredHost in configuredHosts)
            {
                var host = configuredHost.Trim().TrimEnd('.').ToLowerInvariant();
                if (host == "*" || host.Contains('*'))
                    throw new InvalidOperationException("When passkeys are enabled, AllowedHosts must contain only explicit DNS host names. Wildcards are not allowed.");

                ValidateDnsHost(host, "AllowedHosts");
                if (!normalizedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
                    normalizedHosts.Add(host);
            }

            if (!normalizedHosts.Contains(passkeyDomain, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("When passkeys are enabled, AllowedHosts must include Auth:Passkeys:ServerDomain.");

            configuredHosts = normalizedHosts.ToArray();
        }

        configuration["AllowedHosts"] = string.Join(';', configuredHosts);
        services.Configure<IdentityPasskeyOptions>(options =>
        {
            options.ServerDomain = passkeyDomain;
            options.UserVerificationRequirement = "required";
            options.ResidentKeyRequirement = "required";
        });

        return passkeyDomain;
    }

    private static void ValidateDnsHost(string host, string settingName)
    {
        if (Uri.CheckHostName(host) != UriHostNameType.Dns)
            throw new InvalidOperationException($"{settingName} must contain valid DNS host names.");
    }
}
