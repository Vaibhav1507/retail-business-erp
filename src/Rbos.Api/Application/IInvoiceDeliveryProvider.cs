namespace Rbos.Api.Application;

/// <summary>
/// Provider boundary for invoice delivery. The pilot registers only the Meta WhatsApp
/// implementation; a future email provider can implement the same contract.
/// </summary>
public interface IInvoiceDeliveryProvider
{
    string Channel { get; }
    Task<string> SendInvoiceAsync(string recipient, string invoiceNo, byte[] pdf, CancellationToken ct);
}
