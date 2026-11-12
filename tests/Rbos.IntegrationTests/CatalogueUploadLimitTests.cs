using System.Net;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Rbos.IntegrationTests;

public class CatalogueUploadLimitTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    const int Mb = 1024 * 1024;

    static byte[] TablePdf()
    {
        var b = new PdfDocumentBuilder();
        var font = b.AddStandard14Font(Standard14Font.Helvetica);
        var page = b.AddPage(595, 842);
        var rows = new[] { "Model  Name  Category", "FHM1207  LG Washer 7kg  Washing Machine", "T9585  LG Top Load 9kg  Washing Machine" };
        for (var i = 0; i < rows.Length; i++) page.AddText(rows[i], 10, new UglyToad.PdfPig.Core.PdfPoint(40, 780 - i * 20), font);
        return b.Build();
    }

    static byte[] Padded(byte[] pdf, int totalBytes)
    {
        var all = new byte[totalBytes];
        pdf.CopyTo(all, 0);
        for (var i = pdf.Length; i < totalBytes; i++) all[i] = (byte)'\n';
        return all;
    }

    static async Task<Client> NewClient(ApiFactory f)
    {
        var c = new Client(f);
        await c.SignUpAndBootstrap();
        return c;
    }

    static Task<HttpResponseMessage> Read(Client c, byte[] bytes) =>
        c.Send(HttpMethod.Post, "/products/import/read", new { fileName = "catalogue.pdf", contentType = "application/pdf", data = Convert.ToBase64String(bytes) });

    [Fact]
    public async Task Valid_pdf_below_the_limit_is_read()
    {
        var c = await NewClient(f);
        var r = await Read(c, TablePdf());
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        Assert.True(json.GetProperty("rows").GetArrayLength() >= 2);
        Assert.Equal(1, json.GetProperty("pages")[0].GetInt32());
        // CI does not configure Gemini: searchable PDFs must remain uploadable via local extraction.
        Assert.False(json.GetProperty("usedAi").GetBoolean());
        Assert.Contains("map its columns manually", json.GetProperty("warning").GetString());
    }

    [Fact]
    public async Task Pdf_larger_than_the_old_8mb_limit_is_now_accepted()
    {
        var c = await NewClient(f);
        var r = await Read(c, Padded(TablePdf(), 9 * Mb));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task File_just_over_12mb_gets_a_clear_reader_error()
    {
        var c = await NewClient(f);
        var r = await Read(c, Padded(TablePdf(), 12 * Mb + 1));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("12 MB", body);
        Assert.Contains("Split", body);
    }

    [Fact]
    public async Task Request_far_over_12mb_is_rejected_with_actionable_413()
    {
        var c = await NewClient(f);
        var r = await Read(c, Padded(TablePdf(), 14 * Mb));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);
        var body = await r.Content.ReadAsStringAsync();
        Assert.Contains("12 MB", body);
        Assert.Contains("Split", body);
    }

    [Fact]
    public async Task Catalogue_read_still_requires_authentication()
    {
        var c = new Client(f);
        var r = await Read(c, TablePdf());
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Unrelated_endpoints_keep_the_64kb_limit()
    {
        var c = await NewClient(f);
        var big = new string('x', 70 * 1024);
        var create = await c.Send(HttpMethod.Post, "/products", new { name = big, categoryName = "Cables", isGeneric = true, baseUnit = "pcs" });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, create.StatusCode);
        Assert.Contains("Request is too large", await create.Content.ReadAsStringAsync());
        var preview = await c.Send(HttpMethod.Post, "/products/import/preview", new { rows = new[] { new { name = big } } });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, preview.StatusCode);
        var commit = await c.Send(HttpMethod.Post, "/products/import/commit", new { rows = new[] { new { name = big } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, commit.StatusCode);
    }

    [Fact]
    public async Task Invoice_extract_limit_is_unchanged_at_12mb()
    {
        var c = await NewClient(f);
        var r = await c.Send(HttpMethod.Post, "/purchases/extract", new { data = new string('A', 13 * Mb) });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);
        Assert.Contains("Request is too large", await r.Content.ReadAsStringAsync());
    }
}
