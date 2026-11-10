using System.IO.Compression;
using System.Text;

namespace Rbos.Api.Application;

/// <summary>Dependency-free CSV and text-PDF table extraction. Scanned PDFs (images) are not handled here; OCR/AI stays an optional extension.</summary>
public static class CatalogueFileParsers
{
    public static List<List<string>> ParseCsv(string text)
    {
        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        var firstLine = text.Split('\n', 2)[0];
        var delim = new[] { ',', ';', '\t', '|' }.OrderByDescending(d => firstLine.Count(c => c == d)).First();
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == delim) { row.Add(cell.ToString().Trim()); cell.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString().Trim()); cell.Clear();
                rows.Add(row); row = [];
            }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString().Trim()); rows.Add(row); }
        return rows;
    }

    public static bool IsPdf(byte[] b) => b.Length > 5 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;

    /// <summary>Extracts text lines from the PDF content streams and splits them into cells (tabs, 2+ spaces or separate text runs).</summary>
    public static List<List<string>> ReadPdf(byte[] bytes) => ReadPdfPaged(bytes).Select(r => r.Cells).ToList();

    public sealed record PdfRow(int Page, List<string> Cells);

    /// <summary>Actual PDF page numbers via PdfPig; falls back to the legacy stream heuristic only if the PDF cannot be parsed.</summary>
    public static List<PdfRow> ReadPdfPaged(byte[] bytes)
    {
        var pages = PdfPageReader.Read(bytes);
        return pages is null ? ReadPdfPagedLegacy(bytes) : pages.SelectMany(p => p.Rows.Select(r => new PdfRow(p.Page, r))).ToList();
    }

    /// <summary>Legacy best-effort: each text-bearing content stream is treated as one page.</summary>
    static List<PdfRow> ReadPdfPagedLegacy(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var result = new List<PdfRow>();
        var page = 0;
        var pos = 0;
        while (true)
        {
            var s = raw.IndexOf("stream", pos, StringComparison.Ordinal);
            if (s < 0) break;
            var start = s + 6;
            if (start < raw.Length && raw[start] == '\r') start++;
            if (start < raw.Length && raw[start] == '\n') start++;
            var e = raw.IndexOf("endstream", start, StringComparison.Ordinal);
            if (e < 0) break;
            pos = e + 9;
            var dictStart = raw.LastIndexOf("obj", s, StringComparison.Ordinal);
            var dict = dictStart >= 0 ? raw[dictStart..s] : "";
            var data = Encoding.Latin1.GetBytes(raw[start..e]);
            string content;
            if (dict.Contains("FlateDecode"))
            {
                try
                {
                    using var z = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
                    using var o = new MemoryStream();
                    z.CopyTo(o);
                    if (o.Length > 20L * 1024 * 1024) continue;
                    content = Encoding.Latin1.GetString(o.ToArray());
                }
                catch (InvalidDataException) { continue; }
            }
            else content = Encoding.Latin1.GetString(data);
            if (!content.Contains("BT")) continue;
            var lines = new List<List<string>>();
            ExtractText(content, lines);
            if (lines.Count == 0) continue;
            page++;
            foreach (var l in lines) result.Add(new PdfRow(page, l));
        }
        return result;
    }

    /// <summary>Generic document type guess from wording: dealer_scheme, price_list or catalogue.</summary>
    public static string ClassifyDocument(IEnumerable<List<string>> rows)
    {
        var all = rows.Select(r => string.Join(' ', r).ToLowerInvariant()).ToList();
        var text = string.Join(' ', all);
        string[] scheme = ["scheme", "cashback", "dealer", "billing price", "nlc", "offer valid", "incentive", "payout"];
        string[] price = ["price list", "rate list", "dp ", "mop"];
        var schemeRows = all.Count(r => scheme.Any(r.Contains));
        var strongScheme = (text.Contains("scheme for") && text.Contains("offer valid")) || text.Contains("billing price");
        if (scheme.Count(text.Contains) >= 2 && ((schemeRows >= 2 && schemeRows * 10 >= all.Count) || strongScheme)) return "dealer_scheme";
        if (price.Any(text.Contains) && all.Count(r => price.Any(r.Contains)) * 4 >= all.Count) return "price_list";
        return "catalogue";
    }

    static void ExtractText(string c, List<List<string>> lines)
    {
        var cells = new List<string>();
        void Flush() { if (cells.Count > 0) { lines.Add(SplitCells(cells)); cells = []; } }
        var i = 0;
        var lastOp = new StringBuilder();
        while (i < c.Length)
        {
            var ch = c[i];
            if (ch == '(')
            {
                var (str, next) = ReadLiteral(c, i);
                i = next;
                var op = PeekOp(c, i);
                if (op is "Tj" or "'" or "\"") { if (op != "Tj") Flush(); if (str.Trim().Length > 0) cells.Add(str.Trim()); }
            }
            else if (ch == '[')
            {
                var end = c.IndexOf(']', i);
                if (end < 0) break;
                var sb = new StringBuilder();
                for (var j = i + 1; j < end; j++)
                {
                    if (c[j] == '(') { var (str, nx) = ReadLiteral(c, j); sb.Append(str); j = nx - 1; }
                    else if (c[j] == '-' || char.IsDigit(c[j]))
                    {
                        var k = j; while (k < end && (char.IsDigit(c[k]) || c[k] is '-' or '.')) k++;
                        if (double.TryParse(c[j..k], System.Globalization.CultureInfo.InvariantCulture, out var kern) && kern < -250) sb.Append(' ');
                        j = k - 1;
                    }
                }
                i = end + 1;
                if (PeekOp(c, i) == "TJ" && sb.ToString().Trim().Length > 0) cells.Add(sb.ToString().Trim());
            }
            else if (char.IsLetter(ch) || ch is '*')
            {
                var k = i; while (k < c.Length && (char.IsLetter(c[k]) || c[k] is '*')) k++;
                var op = c[i..k];
                if (op is "ET" or "T*") Flush();
                else if (op is "Td" or "TD" or "Tm")
                {
                    var ty = LastNumbers(c, i);
                    if (op == "Tm" ? true : Math.Abs(ty) > 0.01) { if (op == "Tm" && Math.Abs(ty) < 0.01) { } else Flush(); }
                }
                i = k;
            }
            else i++;
        }
        Flush();
        _ = lastOp;
    }

    static double LastNumbers(string c, int opIndex)
    {
        var s = Math.Max(0, opIndex - 80);
        var parts = c[s..opIndex].Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && double.TryParse(parts[^1], System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    static string PeekOp(string c, int i)
    {
        while (i < c.Length && char.IsWhiteSpace(c[i])) i++;
        var k = i; while (k < c.Length && !char.IsWhiteSpace(c[k]) && c[k] is not ('(' or '[' or '<' or '/')) k++;
        return c[i..k];
    }

    static (string, int) ReadLiteral(string c, int i)
    {
        var sb = new StringBuilder();
        var depth = 0;
        for (; i < c.Length; i++)
        {
            var ch = c[i];
            if (ch == '\\' && i + 1 < c.Length)
            {
                i++;
                sb.Append(c[i] switch { 'n' => '\n', 'r' => '\r', 't' => '\t', 'b' => '\b', 'f' => '\f', _ => c[i] });
            }
            else if (ch == '(') { if (depth++ > 0) sb.Append(ch); }
            else if (ch == ')') { if (--depth == 0) return (sb.ToString(), i + 1); sb.Append(ch); }
            else sb.Append(ch);
        }
        return (sb.ToString(), i);
    }

    static List<string> SplitCells(List<string> cells)
    {
        if (cells.Count > 1) return cells;
        return System.Text.RegularExpressions.Regex.Split(cells[0], @"\t|\s{2,}").Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
    }
}
