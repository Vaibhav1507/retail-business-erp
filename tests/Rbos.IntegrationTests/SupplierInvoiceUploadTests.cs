using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rbos.IntegrationTests;

public sealed class SupplierInvoiceUploadTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    const int Mb = 1024 * 1024;

    static async Task<Client> NewClient(ApiFactory factory)
    {
        var client = new Client(factory);
        await client.SignUpAndBootstrap();
        return client;
    }

    static async Task<HttpResponseMessage> SendMultipart(
        Client client, params (string Field, string Mime, byte[] Bytes)[] files)
    {
        var csrf = await client.Raw("/csrf");
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/purchases/extract");
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var form = new MultipartFormDataContent();
        var index = 0;
        foreach (var file in files)
        {
            var part = new ByteArrayContent(file.Bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(file.Mime);
            form.Add(part, file.Field, $"page-{++index}.jpg");
        }
        request.Content = form;
        return await client.SendRaw(request);
    }

    [Fact]
    public async Task Multipart_upload_rejects_wrong_field_name_before_provider_call()
    {
        var client = await NewClient(factory);
        var response = await SendMultipart(client, ("file", "image/jpeg", new byte[] { 1, 2, 3 }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("pages", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Multipart_upload_rejects_more_than_six_pages_before_provider_call()
    {
        var client = await NewClient(factory);
        var files = Enumerable.Range(0, 7)
            .Select(_ => ("pages", "image/jpeg", new byte[] { 1, 2, 3 }))
            .ToArray();
        var response = await SendMultipart(client, files);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at most 6", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Multipart_upload_rejects_a_page_over_eight_megabytes()
    {
        var client = await NewClient(factory);
        var response = await SendMultipart(client,
            ("pages", "application/pdf", new byte[8 * Mb + 1]));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Multipart_upload_rejects_aggregate_over_twelve_megabytes()
    {
        var client = await NewClient(factory);
        var page = new byte[6 * Mb + 1];
        var response = await SendMultipart(client,
            ("pages", "application/pdf", page),
            ("pages", "application/pdf", page));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("12 MB", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Multipart_upload_validates_the_claimed_file_signature()
    {
        var client = await NewClient(factory);
        var response = await SendMultipart(client, ("pages", "image/jpeg", new byte[] { 1, 2, 3, 4 }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("type it claims to be", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Legacy_json_base64_invoice_contract_remains_accepted_by_the_endpoint()
    {
        var client = await NewClient(factory);
        var response = await client.Send(HttpMethod.Post, "/purchases/extract",
            new { contentType = "image/jpeg", data = "AQID" });
        // The request is parsed by the compatibility path, then rejected for its
        // invalid JPEG signature. It must not be mistaken for a missing body.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("body is missing", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
