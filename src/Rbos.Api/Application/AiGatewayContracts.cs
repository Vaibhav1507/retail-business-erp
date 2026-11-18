using System.Net;
using System.Text.Json.Nodes;

namespace Rbos.Api.Application;

public enum AiProviderKind { Gemini, Groq }
public enum AiTaskKind { StructuredExtraction, TextClassification, TextGeneration, AudioTranscription }
public enum AiCapability { Text, StructuredJson, ImageUnderstanding, DocumentExtraction, AudioTranscription }
public enum AiBillingMode { Unknown, Free, Paid }
public enum AiFailureCategory
{
    RateLimited, InvalidCredential, PermissionDenied, BillingDisabled, ProviderUnavailable,
    Timeout, Network, InvalidRequest, UnsupportedModel, ContentSafetyRefusal,
    InvalidStructuredOutput, BudgetExceeded, Disabled, NoEligibleProvider
}

public sealed record AiMedia(string MimeType, byte[] Bytes, int? PageCount = null);

/// <summary>Trusted server-side request context. Tenant and actor IDs are never accepted from browser payloads as authority.</summary>
public sealed record AiGatewayRequest
{
    public required Guid TenantId { get; init; }
    public required Guid ActorUserId { get; init; }
    public required string Feature { get; init; }
    public required AiTaskKind Task { get; init; }
    public required string Prompt { get; init; }
    public string? SystemPrompt { get; init; }
    public string? InputText { get; init; }
    public JsonObject? ResponseSchema { get; init; }
    public IReadOnlyList<AiMedia> Media { get; init; } = [];
    public byte[]? Audio { get; init; }
    public string AudioMimeType { get; init; } = "audio/wav";
    public string? AudioPrompt { get; init; }
    public IReadOnlyList<AiProviderKind> ProviderOrder { get; init; } = [];
    public IReadOnlyDictionary<AiProviderKind, IReadOnlyList<string>> Models { get; init; }
        = new Dictionary<AiProviderKind, IReadOnlyList<string>>();
    public int MaxOutputTokens { get; init; } = 1024;
    public int? EstimatedInputTokens { get; init; }
    public double? AudioDurationSeconds { get; init; }
    public string? CorrelationId { get; init; }
    public int MaxAttemptsPerProvider { get; init; } = 3;

    public AiCapability RequiredCapability =>
        Task == AiTaskKind.AudioTranscription ? AiCapability.AudioTranscription :
        Media.Any(x => x.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)) ? AiCapability.DocumentExtraction :
        Media.Count > 0 ? AiCapability.ImageUnderstanding :
        ResponseSchema is not null ? AiCapability.StructuredJson : AiCapability.Text;
}

public sealed record AiProviderResponse(
    JsonNode? StructuredContent,
    string? Text,
    int? InputTokens,
    int? OutputTokens);

public sealed record AiGatewayResponse(
    JsonNode? StructuredContent,
    string? Text,
    string Provider,
    string Model,
    string CredentialId,
    int? InputTokens,
    int? OutputTokens,
    string CorrelationId);

/// <summary>Credential wrapper deliberately redacts its secret from ToString and log formatting.</summary>
public sealed class AiProviderCredential
{
    public AiProviderCredential(
        string id, AiProviderKind provider, string quotaBucketId, string apiKey,
        AiBillingMode billingMode, IReadOnlySet<AiCapability> capabilities,
        IReadOnlyList<string> allowedModels, decimal? inputCostPerMillionInr,
        decimal? outputCostPerMillionInr, decimal? imageCostPerImageInr,
        decimal? audioCostPerMinuteInr)
    {
        Id = id;
        Provider = provider;
        QuotaBucketId = quotaBucketId;
        ApiKey = apiKey;
        BillingMode = billingMode;
        Capabilities = capabilities;
        AllowedModels = allowedModels;
        InputCostPerMillionInr = inputCostPerMillionInr;
        OutputCostPerMillionInr = outputCostPerMillionInr;
        ImageCostPerImageInr = imageCostPerImageInr;
        AudioCostPerMinuteInr = audioCostPerMinuteInr;
    }

    public string Id { get; }
    public AiProviderKind Provider { get; }
    public string QuotaBucketId { get; }
    public AiBillingMode BillingMode { get; }
    public IReadOnlySet<AiCapability> Capabilities { get; }
    public IReadOnlyList<string> AllowedModels { get; }
    public decimal? InputCostPerMillionInr { get; }
    public decimal? OutputCostPerMillionInr { get; }
    public decimal? ImageCostPerImageInr { get; }
    public decimal? AudioCostPerMinuteInr { get; }
    public string ApiKey { get; }

    public bool Supports(AiCapability capability, string model) =>
        Capabilities.Contains(capability) &&
        (AllowedModels.Count == 0 || AllowedModels.Contains(model, StringComparer.OrdinalIgnoreCase));

    public bool HasPaidPricing =>
        InputCostPerMillionInr is > 0 || OutputCostPerMillionInr is > 0 ||
        ImageCostPerImageInr is > 0 || AudioCostPerMinuteInr is > 0;

    public bool HasCompletePricing(AiCapability capability) =>
        capability == AiCapability.AudioTranscription
            ? AudioCostPerMinuteInr is > 0
            : InputCostPerMillionInr is > 0 && OutputCostPerMillionInr is > 0;

    public bool CanEstimatePaidCost(AiGatewayRequest request)
    {
        if (!HasPaidPricing) return false;
        if (request.Media.Any(m => m.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) && m.PageCount is null)) return false;
        if (request.Audio is not null)
            return request.AudioDurationSeconds is > 0 && AudioCostPerMinuteInr is > 0;
        if (request.Media.Count > 0 && ImageCostPerImageInr is not > 0 &&
            (InputCostPerMillionInr is not > 0 || request.EstimatedInputTokens is null))
            return false;
        return EstimateCost(request) > 0;
    }

    public decimal EstimateCost(AiGatewayRequest request)
    {
        if (BillingMode == AiBillingMode.Free) return 0m;
        var mediaTokens = request.Media.Sum(m => Math.Clamp(m.PageCount ?? 1, 1, 100) * 8192);
        var promptCharacters = request.Prompt.Length + (request.SystemPrompt?.Length ?? 0)
            + (request.InputText?.Length ?? 0) + (request.AudioPrompt?.Length ?? 0)
            + (request.ResponseSchema?.ToJsonString().Length ?? 0);
        var conservativeTextTokens = Math.Clamp((int)Math.Ceiling(promptCharacters / 3d) + mediaTokens, 1, 1_000_000);
        var input = Math.Max(request.EstimatedInputTokens ?? 0, conservativeTextTokens);
        var output = Math.Clamp(request.MaxOutputTokens, 1, 32_000);
        var estimate = input * (InputCostPerMillionInr ?? 0m) / 1_000_000m
            + output * (OutputCostPerMillionInr ?? 0m) / 1_000_000m;
        if (request.Media.Count > 0)
            estimate += request.Media.Sum(m => Math.Clamp(m.PageCount ?? 1, 1, 100)) * (ImageCostPerImageInr ?? 0m);
        if (request.Audio is not null && request.AudioDurationSeconds is > 0)
            estimate += (decimal)(request.AudioDurationSeconds.Value / 60d) * (AudioCostPerMinuteInr ?? 0m);
        return decimal.Ceiling(Math.Max(0m, estimate) * 1.25m * 1_000_000m) / 1_000_000m;
    }

    public decimal? ActualCost(int? inputTokens, int? outputTokens, AiGatewayRequest request)
    {
        if (BillingMode == AiBillingMode.Free) return 0m;
        if (inputTokens is null && outputTokens is null && request.Audio is null &&
            (request.Media.Count == 0 || ImageCostPerImageInr is not > 0)) return null;
        var cost = (inputTokens ?? 0) * (InputCostPerMillionInr ?? 0m) / 1_000_000m
            + (outputTokens ?? 0) * (OutputCostPerMillionInr ?? 0m) / 1_000_000m;
        if (request.Media.Count > 0) cost += request.Media.Sum(m => Math.Clamp(m.PageCount ?? 1, 1, 100)) * (ImageCostPerImageInr ?? 0m);
        if (request.Audio is not null && request.AudioDurationSeconds is > 0)
            cost += (decimal)(request.AudioDurationSeconds.Value / 60d) * (AudioCostPerMinuteInr ?? 0m);
        return decimal.Ceiling(Math.Max(0m, cost) * 1_000_000m) / 1_000_000m;
    }

    public override string ToString() => $"{Provider}:{Id} ({QuotaBucketId})";
}

public sealed class AiProviderException(
    AiFailureCategory category, string safeMessage, HttpStatusCode? statusCode = null,
    TimeSpan? retryAfter = null, Exception? inner = null) : Exception(safeMessage, inner)
{
    public AiFailureCategory Category { get; } = category;
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

public sealed record AiBudgetReservation(Guid Id, bool Allowed, decimal GlobalMonthToDateInr,
    decimal TenantMonthToDateInr, bool WarningThresholdReached, string? BlockReason);

public interface IAiProviderAdapter
{
    AiProviderKind Provider { get; }
    bool Supports(AiCapability capability);
    Task<AiProviderResponse> ExecuteAsync(
        AiGatewayRequest request, AiProviderCredential credential, string model, CancellationToken cancellationToken);
}

public interface IAiGatewayUsageLedger
{
    Task<AiBudgetReservation> ReserveAsync(
        AiGatewayRequest request, AiProviderCredential credential, string model, decimal estimatedCost, CancellationToken ct);
    Task CompleteAsync(
        AiGatewayRequest request, Guid reservationId, string outcome, int? inputTokens, int? outputTokens,
        decimal? actualCost, int latencyMilliseconds, string? errorCategory, int retryCount, CancellationToken ct);
}
