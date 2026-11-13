namespace Rbos.Api.Application;

/// <summary>
/// Provider boundary for invoice delivery. The pilot registers only the Meta WhatsApp
/// implementation; a future email provider can implement the same contract.
/// </summary>
public interface IInvoiceDeliveryProvider
{
    string Channel { get; }
    Task<string> UploadInvoicePdfAsync(byte[] pdf, string invoiceNo, CancellationToken ct);
    Task<string> SendInvoiceTemplateAsync(string recipient, string mediaId, string invoiceNo, CancellationToken ct);
    Task<string> SendInvoiceAsync(string recipient, string invoiceNo, byte[] pdf, CancellationToken ct);
}
