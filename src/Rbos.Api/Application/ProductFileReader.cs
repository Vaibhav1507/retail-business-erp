using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace Rbos.Api.Application;

public sealed record ProductFileResult(
	List<List<string>> Rows,
	bool UsedAi = false,
	List<int>? Pages = null,
	string DocumentKind = "catalogue",
	string? ContentHash = null,
	bool Incomplete = false,
	string? Warning = null);

/// <summary>
/// Turns an uploaded product list into plain table rows (header row first).
/// Excel and Word files are parsed locally; only photos are sent to Gemini.
/// </summary>
public sealed class ProductFileReader(HttpClient http, IConfiguration cfg, ILogger<ProductFileReader> log, LocalOcr ocr)
{
	public const int MaxFileBytes = 12 * 1024 * 1024;
	const long MaxEntryBytes = 20L * 1024 * 1024;
	const int MaxRows = 200;
	const int MaxCols = 30;

	static readonly string[] Header = ["Product Name", "Category", "Unit", "Brand", "Model", "Colour", "Capacity", "Connector Type", "Compatible With"];
	static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
	static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

	const string Prompt = "This is a photo of a product list, price list or stock sheet from an electronics and mobile accessories shop. "
		+ "Return one entry per product. Fields: name, category (e.g. Chargers, Cables, Earphones, Mobile Phones, Power Banks, Speakers, Smartwatches, Cases and Guards, Memory), "
		+ "unit (pcs, set, pack or m; default pcs), brand, model, colour, capacity (e.g. 10000mAh, 64GB), connectorType (e.g. USB-C), compatibleWith. "
		+ "Only use what is visible; use null when a field is not shown. Do not invent products. Ignore prices and quantities.";

	public async Task<ServiceResult<ProductFileResult>> ReadAsync(string? fileName, string? contentType, string? b64, CancellationToken ct)
	{
		if (string.IsNullOrEmpty(b64) || b64.Length > MaxFileBytes / 3 * 4 + 8) return ServiceResult<ProductFileResult>.Fail("File is empty or larger than 12 MB. Split the PDF into smaller parts or compress it, then upload again.");
		byte[] bytes;
		try { bytes = Convert.FromBase64String(b64); } catch (FormatException) { return ServiceResult<ProductFileResult>.Fail("File could not be read."); }
		if (bytes.Length is 0 or > MaxFileBytes) return ServiceResult<ProductFileResult>.Fail("File is empty or larger than 12 MB. Split the PDF into smaller parts or compress it, then upload again.");

		var name = (fileName ?? "").ToLowerInvariant();
		var isZip = bytes.Length > 3 && bytes[0] == 0x50 && bytes[1] == 0x4B;
		try
		{
			if (name.EndsWith(".xlsx") && isZip) return Finish(ReadXlsx(bytes), false);
			if (name.EndsWith(".docx") && isZip) return Finish(ReadDocx(bytes), false);
			if (name.EndsWith(".csv") || name.EndsWith(".txt") || contentType is "text/csv") return Finish(CatalogueFileParsers.ParseCsv(System.Text.Encoding.UTF8.GetString(bytes)), false);
			if ((name.EndsWith(".pdf") || contentType is "application/pdf") && CatalogueFileParsers.IsPdf(bytes))
			{
				var (paged, skippedPages) = await ReadPdfWithOcrAsync(bytes, ct);
				var pdf = paged.Select(r => r.Cells).ToList();
				if (pdf.Count >= 2)
				{
					var incomplete = paged.Count > MaxRows;
					var kept = paged.Take(MaxRows).ToList();
					return ServiceResult<ProductFileResult>.Ok(new(
						kept.Select(r => r.Cells).ToList(),
						false,
						kept.Select(r => r.Page).ToList(),
						CatalogueFileParsers.ClassifyDocument(pdf),
						Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
						incomplete,
						incomplete ? $"Only the first {MaxRows} rows were read; the rest of this PDF was NOT extracted. Split the PDF by page range or onboard the remaining products by CSV/Excel." : null));
				}
				return ServiceResult<ProductFileResult>.Fail(skippedPages.Count > 0 ? $"This PDF has scanned pages ({string.Join(", ", skippedPages.Take(10))}) and local OCR (Tesseract) is not installed or configured (Ocr:TesseractPath). Install Tesseract or upload Excel/CSV." : "No readable text table was found in this PDF. Upload Excel/CSV or a photo instead.");
			}
			if (name.EndsWith(".xls") || name.EndsWith(".doc")) return ServiceResult<ProductFileResult>.Fail("Old .xls / .doc files are not supported. Open the file, choose Save As .xlsx / .docx (or CSV), then upload again.");
			if (contentType is "image/jpeg" or "image/png" or "image/webp" && LooksLikeImage(bytes, contentType))
			{
				var local = await ocr.ReadImageAsync(bytes, ct);
				if (local is { Count: >= 2 }) return Finish(local, false);
				return await ReadImageAsync(contentType, b64, ct);
			}
		}
		catch (Exception e) when (e is InvalidDataException or XmlException or IOException)
		{
			log.LogWarning(e, "Product file could not be parsed");
			return ServiceResult<ProductFileResult>.Fail("This file could not be read. Check that it is a valid Excel (.xlsx) or Word (.docx) file with the product list as a table.");
		}
		return ServiceResult<ProductFileResult>.Fail("Upload an Excel (.xlsx), Word (.docx), CSV or PDF file, or a photo (JPEG, PNG or WebP).");
	}

	/// <summary>Text pages come from the PDF; pages with no text layer are rendered and OCR'd locally. Page numbers are the actual PDF page numbers.</summary>
	async Task<(List<CatalogueFileParsers.PdfRow> Rows, List<int> Skipped)> ReadPdfWithOcrAsync(byte[] bytes, CancellationToken ct)
	{
		var skipped = new List<int>();
		var pages = PdfPageReader.Read(bytes);
		if (pages is null) return (CatalogueFileParsers.ReadPdfPaged(bytes), skipped);
		var rows = new List<CatalogueFileParsers.PdfRow>();
		foreach (var p in pages)
		{
			var cells = p.Rows;
			if (p.NeedsOcr)
			{
				if (!ocr.Available) { log.LogWarning("PDF page {Page} has no text layer and local OCR (Tesseract) is not available; page skipped", p.Page); skipped.Add(p.Page); continue; }
				var png = PdfPageReader.RenderPng(bytes, p.Page);
				cells = png is null ? [] : await ocr.ReadImageAsync(png, ct) ?? [];
			}
			rows.AddRange(cells.Select(c => new CatalogueFileParsers.PdfRow(p.Page, c)));
		}
		return (rows, skipped);
	}

	static ServiceResult<ProductFileResult> Finish(List<List<string>> rows, bool ai)
	{
		rows = rows.Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).Take(MaxRows + 1).ToList();
		return rows.Count < 2
			? ServiceResult<ProductFileResult>.Fail("The file needs a header row and at least one product row.")
			: ServiceResult<ProductFileResult>.Ok(new(rows, ai));
	}

	static bool LooksLikeImage(byte[] b, string mime) => mime switch
	{
		"image/jpeg" => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8,
		"image/png" => b.Length > 4 && b[0] == 0x89 && b[1] == 0x50,
		"image/webp" => b.Length > 12 && b[0] == 0x52 && b[1] == 0x49 && b[8] == 0x57,
		_ => false
	};

	static XDocument Load(ZipArchiveEntry e)
	{
		if (e.Length > MaxEntryBytes) throw new InvalidDataException("Entry too large");
		using var s = e.Open();
		using var r = XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
		return XDocument.Load(r);
	}

	static int ColIndex(string? cellRef)
	{
		var n = 0;
		foreach (var ch in cellRef ?? "") { if (!char.IsAsciiLetter(ch)) break; n = n * 26 + (char.ToUpperInvariant(ch) - 'A' + 1); }
		return Math.Max(0, n - 1);
	}

	static List<List<string>> ReadXlsx(byte[] bytes)
	{
		using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
		var shared = new List<string>();
		var ss = zip.GetEntry("xl/sharedStrings.xml");
		if (ss is not null) foreach (var si in Load(ss).Descendants(S + "si")) shared.Add(string.Concat(si.Descendants(S + "t").Select(t => t.Value)));

		var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")
			?? zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml")).OrderBy(e => e.FullName).FirstOrDefault()
			?? throw new InvalidDataException("No sheet");

		var result = new List<List<string>>();
		foreach (var row in Load(sheet).Descendants(S + "row"))
		{
			var cells = new Dictionary<int, string>();
			foreach (var c in row.Elements(S + "c"))
			{
				var col = ColIndex(c.Attribute("r")?.Value);
				if (col >= MaxCols) continue;
				var type = c.Attribute("t")?.Value;
				var v = c.Element(S + "v")?.Value ?? "";
				string text = type switch
				{
					"s" => int.TryParse(v, out var i) && i >= 0 && i < shared.Count ? shared[i] : "",
					"inlineStr" => string.Concat(c.Descendants(S + "t").Select(t => t.Value)),
					_ => v
				};
				cells[col] = text.Trim();
			}
			if (cells.Count == 0) continue;
			var width = cells.Keys.Max() + 1;
			result.Add(Enumerable.Range(0, width).Select(i => cells.GetValueOrDefault(i, "")).ToList());
			if (result.Count > MaxRows + 1) break;
		}
		return result;
	}

	static List<List<string>> ReadDocx(byte[] bytes)
	{
		using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
		var entry = zip.GetEntry("word/document.xml") ?? throw new InvalidDataException("No document");
		var table = Load(entry).Descendants(W + "tbl").FirstOrDefault()
			?? throw new InvalidDataException("No table");
		return table.Elements(W + "tr").Take(MaxRows + 1)
			.Select(tr => tr.Elements(W + "tc").Take(MaxCols)
				.Select(tc => string.Join(" ", tc.Descendants(W + "p").Select(p => string.Concat(p.Descendants(W + "t").Select(t => t.Value)))).Trim())
				.ToList())
			.ToList();
	}

	async Task<ServiceResult<ProductFileResult>> ReadImageAsync(string mime, string b64, CancellationToken ct)
	{
		var apiKey = cfg["Gemini:ApiKey"];
		if (string.IsNullOrWhiteSpace(apiKey)) return ServiceResult<ProductFileResult>.Fail("Photo reading is not set up yet. Ask the owner to add the Gemini key, or upload an Excel, Word or CSV file.");
		var models = new[] { cfg["Gemini:Model"] ?? "gemini-3.5-flash", cfg["Gemini:FallbackModel"] ?? "gemini-flash-latest" }.Distinct().ToArray();
		JsonNode? parsed = null;
		foreach (var model in models)
		{
			try { parsed = await CallAsync(model, apiKey, mime, b64, ct); break; }
			catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
			{ return ServiceResult<ProductFileResult>.Fail("Free photo reading limit reached. Try again later or upload an Excel / CSV file."); }
			catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
			{ log.LogWarning(e, "Product photo extraction failed on {Model}", model); }
		}
		if (parsed?["products"] is not JsonArray items) return ServiceResult<ProductFileResult>.Fail("Could not read the photo. Try a clearer, well-lit picture or upload a file.");

		static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : "";
		var rows = new List<List<string>> { Header.ToList() };
		foreach (var n in items)
			rows.Add([Str(n?["name"]), Str(n?["category"]), Str(n?["unit"]), Str(n?["brand"]), Str(n?["model"]), Str(n?["colour"]), Str(n?["capacity"]), Str(n?["connectorType"]), Str(n?["compatibleWith"])]);
		return Finish(rows, true);
	}

	async Task<JsonNode?> CallAsync(string model, string apiKey, string mime, string b64, CancellationToken ct)
	{
		static JsonObject T() => new() { ["type"] = "STRING", ["nullable"] = true };
		var item = new JsonObject
		{
			["type"] = "OBJECT",
			["properties"] = new JsonObject { ["name"] = T(), ["category"] = T(), ["unit"] = T(), ["brand"] = T(), ["model"] = T(), ["colour"] = T(), ["capacity"] = T(), ["connectorType"] = T(), ["compatibleWith"] = T() }
		};
		var schema = new JsonObject { ["type"] = "OBJECT", ["properties"] = new JsonObject { ["products"] = new JsonObject { ["type"] = "ARRAY", ["items"] = item } } };
		var body = new JsonObject
		{
			["contents"] = new JsonArray(new JsonObject
			{
				["parts"] = new JsonArray(
					new JsonObject { ["inline_data"] = new JsonObject { ["mime_type"] = mime, ["data"] = b64 } },
					new JsonObject { ["text"] = Prompt })
			}),
			["generationConfig"] = new JsonObject { ["responseMimeType"] = "application/json", ["responseSchema"] = schema, ["temperature"] = 0 }
		};
		using var req = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
		req.Headers.Add("x-goog-api-key", apiKey);
		req.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
		using var res = await http.SendAsync(req, ct);
		res.EnsureSuccessStatusCode();
		var root = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct));
		var text = root?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
		return text is null ? null : JsonNode.Parse(text);
	}
}
