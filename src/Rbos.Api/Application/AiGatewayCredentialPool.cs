using System.Collections.Concurrent;

namespace Rbos.Api.Application;

/// <summary>
/// In-memory health state over server-configured credentials. Gemini keys are grouped by project quota;
/// Groq keys are grouped by team quota. Round-robin advances on every gateway request.
/// </summary>
public sealed class AiGatewayCredentialPool
{
    private readonly IConfiguration _configuration;
    private readonly IReadOnlyList<AiProviderCredential> _credentials;
    private readonly ConcurrentDictionary<string, long> _cursors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cooldowns = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _invalidCredentials = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CircuitState> _circuits = new(StringComparer.Ordinal);

    public AiGatewayCredentialPool(IConfiguration configuration)
    {
        _configuration = configuration;
        _credentials = LoadCredentials(configuration);
    }

    public IReadOnlyList<AiProviderCredential> SelectOrder(
        AiProviderKind provider, AiCapability capability, string model, bool allowPaid)
    {
        // AiGateway checks the provider circuit once before entering the provider's
        // model/credential fallback loop. Do not re-check it here: a failure on one
        // model must not prevent this same request from trying the remaining models.
        if (!ProviderEnabled(provider)) return [];
        var candidates = _credentials
            .Where(c => c.Provider == provider && c.Supports(capability, model))
            .Where(c => !_invalidCredentials.ContainsKey(c.Id))
            .Where(c => !_cooldowns.TryGetValue(c.QuotaBucketId, out var until) || until <= DateTimeOffset.UtcNow)
            .Where(c => c.BillingMode == AiBillingMode.Free ||
                (c.BillingMode == AiBillingMode.Paid && allowPaid && c.HasCompletePricing(capability)))
            .ToArray();
        if (candidates.Length == 0) return [];

        var key = $"{provider}|{capability}|{model}";
        var cursor = _cursors.AddOrUpdate(key, 1, (_, current) => unchecked(current + 1)) - 1;
        var start = (int)(unchecked((ulong)cursor) % (uint)candidates.Length);
        return Enumerable.Range(0, candidates.Length)
            .Select(i => candidates[(start + i) % candidates.Length]).ToArray();
    }

    public bool IsFeatureProviderAvailable(
        AiProviderKind provider, AiCapability capability, bool allowPaid,
        IReadOnlyList<string>? models = null)
    {
        if (!ProviderEnabled(provider) || IsCircuitOpen(provider)) return false;
        var configuredModels = models is { Count: > 0 } ? models : [DefaultModel(provider, capability)];
        return configuredModels.Any(model => HasEligibleCredentials(provider, capability, model, allowPaid));
    }

    public bool HasEligibleCredentials(AiProviderKind provider, AiCapability capability, string model, bool allowPaid) =>
        ProviderEnabled(provider) && !IsCircuitOpen(provider) &&
        _credentials.Any(c => c.Provider == provider && c.Supports(capability, model) &&
            !_invalidCredentials.ContainsKey(c.Id) &&
            (!_cooldowns.TryGetValue(c.QuotaBucketId, out var until) || until <= DateTimeOffset.UtcNow) &&
            (c.BillingMode == AiBillingMode.Free ||
             (c.BillingMode == AiBillingMode.Paid && allowPaid && c.HasCompletePricing(capability))));

    public bool IsCredentialStillEligible(AiProviderCredential credential, AiCapability capability, string model, bool allowPaid) =>
        // Circuit state is checked once before entering a provider's model loop.
        // Do not let a failure mid-request suppress that request's remaining fallback models.
        ProviderEnabled(credential.Provider) &&
        !_invalidCredentials.ContainsKey(credential.Id) &&
        (!_cooldowns.TryGetValue(credential.QuotaBucketId, out var until) || until <= DateTimeOffset.UtcNow) &&
        credential.Supports(capability, model) &&
        (credential.BillingMode == AiBillingMode.Free ||
         (credential.BillingMode == AiBillingMode.Paid && allowPaid && credential.HasCompletePricing(capability)));

    public void MarkRateLimited(AiProviderCredential credential, TimeSpan? retryAfter)
    {
        var fallbackSeconds = Math.Clamp(_configuration.GetValue("AiGateway:QuotaCooldownSeconds", 60), 5, 3600);
        var duration = retryAfter ?? TimeSpan.FromSeconds(fallbackSeconds);
        duration = TimeSpan.FromSeconds(Math.Clamp(duration.TotalSeconds, 1, 3600));
        _cooldowns[credential.QuotaBucketId] = DateTimeOffset.UtcNow.Add(duration);
    }

    public void MarkCredentialInvalid(AiProviderCredential credential) =>
        _invalidCredentials[credential.Id] = 0;

    public void MarkProviderFailure(AiProviderKind provider)
    {
        var threshold = Math.Clamp(_configuration.GetValue("AiGateway:CircuitBreakerFailureThreshold", 3), 1, 20);
        var state = _circuits.GetOrAdd(provider.ToString(), _ => new CircuitState());
        lock (state)
        {
            state.Failures++;
            if (state.Failures >= threshold)
            {
                var seconds = Math.Clamp(_configuration.GetValue("AiGateway:CircuitBreakerCooldownSeconds", 30), 5, 900);
                state.OpenUntil = DateTimeOffset.UtcNow.AddSeconds(seconds);
                state.Failures = 0;
            }
        }
    }

    public void MarkProviderSuccess(AiProviderKind provider)
    {
        if (!_circuits.TryGetValue(provider.ToString(), out var state)) return;
        lock (state) { state.Failures = 0; state.OpenUntil = null; }
    }

    public bool IsCircuitOpen(AiProviderKind provider)
    {
        if (!_circuits.TryGetValue(provider.ToString(), out var state)) return false;
        lock (state)
        {
            if (state.OpenUntil is null) return false;
            if (state.OpenUntil > DateTimeOffset.UtcNow) return true;
            state.OpenUntil = null;
            state.Failures = 0;
            return false;
        }
    }

    public bool ProviderEnabled(AiProviderKind provider) =>
        _configuration.GetValue($"AiGateway:Providers:{provider}:Enabled", false);

    public static string DefaultModel(AiProviderKind provider, AiCapability capability) =>
        (provider, capability) switch
        {
            (AiProviderKind.Gemini, _) => "gemini-3.5-flash",
            (AiProviderKind.Groq, AiCapability.AudioTranscription) => "whisper-large-v3-turbo",
            (AiProviderKind.Groq, _) => "qwen/qwen3.8-27b",
            _ => "default"
        };

    private static IReadOnlyList<AiProviderCredential> LoadCredentials(IConfiguration config)
    {
        var result = new List<AiProviderCredential>();
        var explicitEntries = config.GetSection("AiGateway:Credentials").GetChildren().ToArray();
        foreach (var item in explicitEntries)
        {
            if (!Enum.TryParse<AiProviderKind>(item["Provider"], true, out var provider)) continue;
            var id = item["Id"]?.Trim();
            var secretReference = item["SecretRef"]?.Trim();
            var secret = string.IsNullOrWhiteSpace(secretReference) ? null : config[secretReference];
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret) ||
                !bool.TryParse(item["Enabled"] ?? "true", out var enabled) || !enabled) continue;

            var billing = Enum.TryParse<AiBillingMode>(item["BillingMode"], true, out var parsedBilling)
                ? parsedBilling : AiBillingMode.Unknown;
            var projectOrTeam = provider == AiProviderKind.Gemini
                ? item["ProjectId"]?.Trim()
                : item["TeamId"]?.Trim();
            // Missing quota identity is deliberately collapsed into one conservative bucket, never treated as independent capacity.
            var bucket = $"{provider}:{(string.IsNullOrWhiteSpace(projectOrTeam) ? "unidentified" : projectOrTeam)}";
            var capabilities = ParseCapabilities(item.GetSection("Capabilities").Get<string[]>(), provider);
            var models = item.GetSection("Models").Get<string[]>() ?? [];
            result.Add(new AiProviderCredential(id, provider, bucket, secret, billing, capabilities, models,
                Decimal(item["InputCostPerMillionInr"]), Decimal(item["OutputCostPerMillionInr"]),
                Decimal(item["ImageCostPerImageInr"]), Decimal(item["AudioCostPerMinuteInr"])));
        }

        foreach (var provider in Enum.GetValues<AiProviderKind>())
        {
            if (result.Any(c => c.Provider == provider)) continue;
            var secret = provider == AiProviderKind.Gemini ? config["Gemini:ApiKey"] : config["Groq:ApiKey"];
            if (string.IsNullOrWhiteSpace(secret)) continue;
            var identity = provider == AiProviderKind.Gemini ? config["Gemini:ProjectId"] : config["Groq:TeamId"];
            var bucket = $"{provider}:{(string.IsNullOrWhiteSpace(identity) ? "unidentified" : identity)}";
            var billingKey = provider == AiProviderKind.Gemini
                ? "AiGateway:LegacyGeminiBillingMode" : "AiGateway:LegacyGroqBillingMode";
            var billing = Enum.TryParse<AiBillingMode>(config[billingKey], true, out var mode)
                ? mode : AiBillingMode.Unknown;
            var prefix = provider == AiProviderKind.Gemini ? "AiGateway:LegacyGemini" : "AiGateway:LegacyGroq";
            result.Add(new AiProviderCredential(
                provider == AiProviderKind.Gemini ? "gemini-legacy" : "groq-legacy",
                provider, bucket, secret, billing, ParseCapabilities(null, provider), [],
                Decimal(config[$"{prefix}:InputCostPerMillionInr"]),
                Decimal(config[$"{prefix}:OutputCostPerMillionInr"]),
                Decimal(config[$"{prefix}:ImageCostPerImageInr"]),
                Decimal(config[$"{prefix}:AudioCostPerMinuteInr"])));
        }

        return result;
    }

    private static decimal? Decimal(string? value) =>
        decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed >= 0 ? parsed : null;

    private static IReadOnlySet<AiCapability> ParseCapabilities(string[]? configured, AiProviderKind provider)
    {
        if (configured is { Length: > 0 })
            return configured.Select(x => Enum.TryParse<AiCapability>(x, true, out var capability)
                    ? capability : (AiCapability?)null)
                .Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        return provider == AiProviderKind.Gemini
            ? new HashSet<AiCapability> { AiCapability.Text, AiCapability.StructuredJson, AiCapability.ImageUnderstanding, AiCapability.DocumentExtraction }
            : Enum.GetValues<AiCapability>().ToHashSet();
    }

    private sealed class CircuitState
    {
        public int Failures;
        public DateTimeOffset? OpenUntil;
    }
}
