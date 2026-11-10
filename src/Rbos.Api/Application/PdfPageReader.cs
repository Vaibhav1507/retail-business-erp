using System.Text;

namespace Rbos.Api.Application;

/// <summary>Reads text per actual PDF page number (PdfPig). Pages with almost no text are flagged so they can be OCR'd.</summary>
public static class PdfPageReader
{
    public sealed record PageText(int Page, List<List<string>> Rows, bool NeedsOcr);

    public static List<PageText>? Read(byte[] bytes)
    {
        try
        {
            using var doc = UglyToad.PdfPig.PdfDocument.Open(bytes);
            var pages = new List<PageText>();
            foreach (var pg in doc.GetPages())
            {
                var words = pg.GetWords().ToList();
                var rows = new List<List<string>>();
                foreach (var line in words.GroupBy(w => Math.Round(w.BoundingBox.Bottom / 4)).OrderByDescending(g => g.Key))
                {
                    var cells = new List<string>();
                    var sb = new StringBuilder();
                    var lastRight = double.NaN;
                    foreach (var w in line.OrderBy(w => w.BoundingBox.Left))
                    {
                        if (!double.IsNaN(lastRight) && w.BoundingBox.Left - lastRight > 12) { cells.Add(sb.ToString().Trim()); sb.Clear(); }
                        sb.Append(w.Text).Append(' ');
                        lastRight = w.BoundingBox.Right;
                    }
                    if (sb.Length > 0) cells.Add(sb.ToString().Trim());
                    cells = cells.Where(c => c.Length > 0).ToList();
                    if (cells.Count > 0) rows.Add(cells);
                }
                pages.Add(new(pg.Number, rows, words.Sum(w => w.Text.Length) < 20));
            }
            return pages;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { return null; }
    }

    /// <summary>Renders one page (1-based) to PNG for OCR. Null if rendering is unavailable.</summary>
    public static byte[]? RenderPng(byte[] pdf, int page)
    {
        try
        {
            using var ms = new MemoryStream();
            using var skia = PDFtoImage.Conversion.ToImage(pdf, page - 1, options: new(Dpi: 200));
            using var data = skia.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            data.SaveTo(ms);
            return ms.ToArray();
        }
        catch (Exception e) when (e is not OutOfMemoryException) { return null; }
    }
}
