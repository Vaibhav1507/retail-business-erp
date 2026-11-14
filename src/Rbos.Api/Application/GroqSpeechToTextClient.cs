using System.Net.Http.Headers;
using System.Text.Json;

namespace Rbos.Api.Application;

/// <summary>Hosted Whisper fallback for short navigation commands. Credentials stay server-side.</summary>
public sealed class GroqSpeechToTextClient(HttpClient http, IConfiguration cfg, ILogger<GroqSpeechToTextClient> log)
{
    const string Endpoint = "https://api.groq.com/openai/v1/audio/transcriptions";
    const int MaxAudioBytes = 1_000_000;

    public bool Available =>
        string.Equals(cfg["Groq:WhisperEnabled"], "true", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(cfg["Groq:ApiKey"]);

    public async Task<string?> TranscribeWavAsync(byte[] wav, CancellationToken ct)
    {
        if (!Available) throw new InvalidOperationException("Hosted Whisper fallback is not enabled.");
        if (wav.Length is <= 44 or > MaxAudioBytes) throw new ArgumentOutOfRangeException(nameof(wav), "Audio must be a non-empty WAV under 1 MB.");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(wav);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(file, "file", "navigation-command.wav");
        form.Add(new StringContent(cfg["Groq:WhisperModel"] ?? "whisper-large-v3-turbo"), "model");
        form.Add(new StringContent("json"), "response_format");
        form.Add(new StringContent("0"), "temperature");
        form.Add(new StringContent("Short navigation command for an Indian retail business app. Speech may mix Hindi, Romanised Hindi, Bundelkhandi and English. Common terms include bill, purchase, stock, customer, supplier, payment, kharcha and udhari."), "prompt");

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg["Groq:ApiKey"]);
        request.Content = form;
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Never log audio, transcript, response body or credentials.
            log.LogWarning("Groq Whisper request failed with HTTP {StatusCode}", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: ct);
        return json.RootElement.TryGetProperty("text", out var text) ? text.GetString()?.Trim() : null;
    }
}
