using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Rbos.IntegrationTests;

public sealed class DocumentIntakeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    static readonly byte[] JpegOne = [0xFF, 0xD8, 0xFF, 0xD9];
    static readonly byte[] JpegTwo = [0xFF, 0xD8, 0xFF, 0xD9, 0x01];

    [Fact]
    public async Task Upload_batch_persists_grouped_pages_and_is_idempotent()
    {
        using var http = factory.CreateClient();
        var client = new Client(http);
        await client.SignUpAndBootstrap();

        var invalid = await UploadAsync(http, "Sale", [new("page-0-0", "bill.jpg", "image/jpeg", [1, 2, 3])], Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var key = Guid.NewGuid().ToString("N");
        var pages = new[]
        {
            new UploadPage("page-0-0", "bill-front.jpg", "image/jpeg", JpegOne),
            new UploadPage("page-0-1", "bill-back.jpg", "image/jpeg", JpegTwo)
        };

        var first = await UploadAsync(http, "Sale", pages, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var firstRoot = firstJson.RootElement;
        var batchId = firstRoot.GetProperty("batchId").GetGuid();
        var document = firstRoot.GetProperty("documents")[0];
        var documentId = document.GetProperty("id").GetGuid();
        Assert.False(firstRoot.GetProperty("replayed").GetBoolean());
        Assert.Equal("Queued", document.GetProperty("status").GetString());
        Assert.Equal(2, document.GetProperty("pageCount").GetInt32());

        var replay = await UploadAsync(http, "Sale", pages, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.True(replayJson.RootElement.GetProperty("replayed").GetBoolean());
        Assert.Equal(batchId, replayJson.RootElement.GetProperty("batchId").GetGuid());
        Assert.Equal(documentId, replayJson.RootElement.GetProperty("documents")[0].GetProperty("id").GetGuid());

        var conflict = await UploadAsync(http, "Sale",
            [new UploadPage("page-0-0", "different-bill.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF, 0xEE])], key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var list = await http.GetFromJsonAsync<JsonElement>("/api/documents?direction=Sale");
        Assert.Single(list.EnumerateArray());

        var pageResponse = await http.GetAsync($"/api/documents/{documentId}/pages/2");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        Assert.Equal(JpegTwo, await pageResponse.Content.ReadAsByteArrayAsync());

        var details = await http.GetFromJsonAsync<JsonElement>($"/api/documents/{documentId}");
        Assert.Equal(2, details.GetProperty("pages").GetArrayLength());
        var history = details.GetProperty("statusHistory").EnumerateArray().ToArray();
        Assert.Single(history);
        Assert.Equal("Queued", history[0].GetProperty("status").GetString());

        // Verify the API-created durable queue row is claimable only through the worker function.
        var cfg = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        await using var worker = new NpgsqlConnection(cfg["ConnectionStrings:RbosTestWhatsAppWorker"]);
        await worker.OpenAsync();
        Guid claimedId;
        Guid leaseToken;
        await using (var claim = new NpgsqlCommand(@"
            select document_id, lease_token
              from integrations.claim_document_processing(1, 300, @document)", worker))
        {
            claim.Parameters.AddWithValue("document", documentId);
            await using var reader = await claim.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            claimedId = reader.GetGuid(0);
            leaseToken = reader.GetGuid(1);
        }
        await using (var finish = new NpgsqlCommand(
            "select integrations.finish_document_processing(@document, @lease, null)", worker))
        {
            finish.Parameters.AddWithValue("document", claimedId);
            finish.Parameters.AddWithValue("lease", leaseToken);
            Assert.True((bool)(await finish.ExecuteScalarAsync())!);
        }
        Assert.Equal(documentId, claimedId);
    }

    [Fact]
    public async Task Documents_and_original_pages_are_isolated_between_tenants()
    {
        using var firstHttp = factory.CreateClient();
        var firstClient = new Client(firstHttp);
        await firstClient.SignUpAndBootstrap();
        var uploaded = await UploadAsync(firstHttp, "Purchase",
            [new UploadPage("page-0-0", "supplier-bill.jpg", "image/jpeg", JpegOne)],
            Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        using var json = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync());
        var documentId = json.RootElement.GetProperty("documents")[0].GetProperty("id").GetGuid();

        using var secondHttp = factory.CreateClient();
        var secondClient = new Client(secondHttp);
        await secondClient.SignUpAndBootstrap();

        var list = await secondHttp.GetFromJsonAsync<JsonElement>("/api/documents");
        Assert.Empty(list.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await secondHttp.GetAsync($"/api/documents/{documentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secondHttp.GetAsync($"/api/documents/{documentId}/pages/1")).StatusCode);
    }

    sealed record UploadPage(string Field, string FileName, string ContentType, byte[] Bytes);

    static async Task<HttpResponseMessage> UploadAsync(
        HttpClient http, string direction, IReadOnlyList<UploadPage> pages, string idempotencyKey)
    {
        var token = await http.GetFromJsonAsync<JsonElement>("/api/csrf");
        var manifest = new
        {
            direction,
            documents = new[]
            {
                new { sourceKind = "HandwrittenBill", pages = pages.Select(x => x.Field).ToArray() }
            }
        };
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(JsonSerializer.Serialize(manifest), Encoding.UTF8, "application/json"), "manifest");
        foreach (var page in pages)
        {
            var file = new ByteArrayContent(page.Bytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(page.ContentType);
            content.Add(file, page.Field, page.FileName);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents/batches");
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = content;
        return await http.SendAsync(request);
    }
}
