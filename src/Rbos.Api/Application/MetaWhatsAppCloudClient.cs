using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Rbos.Api.Application;

/// <summary>Small typed client for the Meta WhatsApp Cloud API invoice workflow.</summary>
public sealed class MetaWhatsAppCloudClient(
    HttpClient http,
    string apiVersion,
    string phoneNumberId,
    string accessToken,
    string templateName,
    string templateLanguage)
{
    public async Task<string> UploadInvoicePdfAsync(byte[] pdf, string invoiceNo, CancellationToken ct)
    {
        if (pdf.Length == 0) throw new ArgumentException("Invoice PDF is empty.", nameof(pdf));
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", SafeFileName(invoiceNo) + ".pdf");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/media") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw MetaWhatsAppApiException.FromResponse(response.StatusCode, body, reviewOnServerError: false);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("id").GetString()
            ?? throw new MetaWhatsAppApiException(false, "Meta media upload did not return a media ID.");
    }

    public async Task<string> SendInvoiceTemplateAsync(string recipient, string mediaId, string invoiceNo, CancellationToken ct)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = recipient,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = templateLanguage },
                components = new object[]
                {
                    new { type = "header", parameters = new object[] { new { type = "document", document = new { id = mediaId, filename = SafeFileName(invoiceNo) + ".pdf" } } } },
                    new { type = "body", parameters = new object[] { new { type = "text", text = invoiceNo } } }
                }
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw MetaWhatsAppApiException.FromResponse(response.StatusCode, body, reviewOnServerError: true);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("messages")[0].GetProperty("id").GetString()
            ?? throw new MetaWhatsAppApiException(false, "Meta send response did not contain a message ID.");
    }

    static string SafeFileName(string value)
    {
        var safe = new string(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').Take(40).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "invoice" : safe;
    }
}

public sealed class MetaWhatsAppApiException(bool retryable, string safeMessage, bool needsReview = false) : Exception(safeMessage)
{
    public bool Retryable { get; } = retryable;
    public bool NeedsReview { get; } = needsReview;
    public string SafeMessage { get; } = safeMessage;

    public static MetaWhatsAppApiException FromResponse(HttpStatusCode status, string body, bool reviewOnServerError)
    {
        var serverError = (int)status >= 500;
        var needsReview = serverError && reviewOnServerError;
        var retryable = status == HttpStatusCode.TooManyRequests || (serverError && !reviewOnServerError);
        var message = "Meta WhatsApp API request failed (HTTP " + (int)status + ").";
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("code", out var code))
                message += " Provider error code: " + code.ToString() + ".";
        }
        catch (JsonException) { }
        return new MetaWhatsAppApiException(retryable, message, needsReview);
    }
}
