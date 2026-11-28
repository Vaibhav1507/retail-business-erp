using Google.Api.Gax.Grpc;
using Google.Cloud.Vision.V1;

namespace Rbos.Api.Application;

public sealed record SalesInvoiceOcrWord(
    string Text,
    int Left,
    int Top,
    int Width,
    int Height);

public sealed record SalesInvoiceOcrPage(
    int PageNumber,
    int Width,
    int Height,
    IReadOnlyList<SalesInvoiceOcrWord> Words);

public sealed record SalesInvoiceOcrResult(
    string Text,
    IReadOnlyList<SalesInvoiceOcrPage> Pages);

public interface ISalesInvoiceOcrReader
{
    Task<SalesInvoiceOcrResult?> ReadAsync(string mime, byte[] originalDocument, CancellationToken cancellationToken);
}

/// <summary>
/// Independent Google Cloud Vision DOCUMENT_TEXT_DETECTION reader for sales bills.
/// ImageAnnotatorClient uses Application Default Credentials, including the Cloud Run
/// service identity. No service-account JSON key or OCR transcript is passed to Haiku.
/// </summary>
public sealed class CloudVisionSalesInvoiceOcrReader(
    IConfiguration configuration,
    ILogger<CloudVisionSalesInvoiceOcrReader> logger) : ISalesInvoiceOcrReader, IDisposable
{
    readonly Lazy<ImageAnnotatorClient> client = new(ImageAnnotatorClient.Create);
    readonly int maxPages = Math.Clamp(configuration.GetValue("CloudVision:SalesInvoiceMaxPages", 6), 1, 6);

    public async Task<SalesInvoiceOcrResult?> ReadAsync(
        string mime,
        byte[] originalDocument,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("CloudVision:Enabled", true))
            return null;

        var pages = new List<(int PageNumber, byte[] Bytes, bool OwnsBytes)>();
        try
        {
            if (mime == "application/pdf")
            {
                var pdfPages = PdfPageReader.Read(originalDocument);
                if (pdfPages is null || pdfPages.Count is < 1 || pdfPages.Count > maxPages)
                {
                    logger.LogWarning("Cloud Vision skipped sales invoice PDF with unreadable or unsupported page count. Max pages {MaxPages}",
                        maxPages);
                    return null;
                }

                foreach (var page in pdfPages)
                {
                    var png = PdfPageReader.RenderPng(originalDocument, page.Page);
                    if (png is null)
                    {
                        logger.LogWarning("Cloud Vision could not render sales invoice PDF page {PageNumber}.", page.Page);
                        return null;
                    }
                    pages.Add((page.Page, png, true));
                }
            }
            else
            {
                pages.Add((1, originalDocument, false));
            }

            using var gate = new SemaphoreSlim(2, 2);
            var tasks = pages.Select(async page =>
            {
                await gate.WaitAsync(cancellationToken);
                try { return await ReadPageAsync(page.PageNumber, page.Bytes, cancellationToken); }
                finally { gate.Release(); }
            }).ToArray();

            var annotations = await Task.WhenAll(tasks);
            if (annotations.Any(page => page is null))
                return null;

            var resultPages = annotations.Select(page => page!).OrderBy(page => page.PageNumber).ToArray();
            var text = string.Join("\n", resultPages.Select(page =>
                string.Join(' ', page.Words.Select(word => word.Text))));
            logger.LogInformation(
                "Cloud Vision sales-invoice OCR completed. Pages {PageCount}, words {WordCount}, text characters {TextCharacters}",
                resultPages.Length, resultPages.Sum(page => page.Words.Count), text.Length);
            return new SalesInvoiceOcrResult(text, resultPages);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Do not log document content, API payloads, credentials or provider error bodies.
            logger.LogWarning("Cloud Vision sales-invoice OCR is unavailable. Error type {ErrorType}.", error.GetType().Name);
            return null;
        }
        finally
        {
            foreach (var page in pages.Where(page => page.OwnsBytes))
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(page.Bytes);
        }
    }

    async Task<SalesInvoiceOcrPage?> ReadPageAsync(
        int pageNumber,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var image = Image.FromBytes(bytes);
        var annotation = await client.Value.DetectDocumentTextAsync(
            image, callSettings: CallSettings.FromCancellationToken(cancellationToken));

        var page = annotation.Pages.FirstOrDefault();
        if (page is null) return null;
        var words = new List<SalesInvoiceOcrWord>();
        foreach (var block in page.Blocks)
        foreach (var paragraph in block.Paragraphs)
        foreach (var word in paragraph.Words)
        {
            var text = string.Concat(word.Symbols.Select(symbol => symbol.Text));
            if (string.IsNullOrWhiteSpace(text)) continue;
            var vertices = word.BoundingBox?.Vertices;
            if (vertices is null || vertices.Count == 0) continue;
            var left = vertices.Min(vertex => vertex.X);
            var top = vertices.Min(vertex => vertex.Y);
            var right = vertices.Max(vertex => vertex.X);
            var bottom = vertices.Max(vertex => vertex.Y);
            words.Add(new SalesInvoiceOcrWord(text, left, top,
                Math.Max(0, right - left), Math.Max(0, bottom - top)));
        }

        return new SalesInvoiceOcrPage(pageNumber, page.Width, page.Height, words);
    }

    public void Dispose()
    {
        if (client.IsValueCreated) client.Value.Dispose();
    }
}
