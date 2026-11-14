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
                var extractedText = string.Join(" ", rows.SelectMany(row => row));
                pages.Add(new(pg.Number, rows, IsUnreliableText(extractedText)));
            }
            return pages;
        }
        catch (Exception e) when (e is not OutOfMemoryException) { return null; }
    }

    /// <summary>
    /// Detects pages whose embedded text layer is present but unusable. Some manufacturer PDFs
    /// encode glyphs with a custom character map: the page looks normal, but extracted words are
    /// shifted/garbled. Such pages must be rendered and OCR'd instead of trusting the text layer.
    /// </summary>
    public static bool IsUnreliableText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        var chars = text.Where(c => !char.IsWhiteSpace(c)).ToArray();
        if (chars.Length < 20) return true;

        var invalid = chars.Count(c => char.IsControl(c) || char.IsSurrogate(c) ||
            char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.PrivateUse or System.Globalization.UnicodeCategory.OtherNotAssigned ||
            c == '\\uFFFD');
        if ((double)invalid / chars.Length >= 0.01) return true;

        var letters = chars.Where(char.IsLetter).ToArray();
        if (letters.Length < 150) return false;

        var vowelRatio = (double)letters.Count(c => "aeiouAEIOU".Contains(c)) / letters.Length;
        var words = System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), @"[a-z]{4,}")
            .Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        var knownCatalogueTerms = new[]
        {
            "resolution", "processor", "picture", "sound", "speaker", "connectivity",
            "remote", "display", "operating", "system", "game", "features", "model",
            "category", "brand", "capacity", "colour", "color", "hdmi", "bluetooth",
            "wireless", "smart", "memory", "storage", "battery", "camera"
        };
        var recognizedTerms = knownCatalogueTerms.Count(words.Contains);

        // A dense specification page with almost no recognizable English labels and an
        // unusually low vowel ratio is a strong signal of a broken embedded font ToUnicode map.
        return recognizedTerms < 6 && vowelRatio < 0.24;
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
