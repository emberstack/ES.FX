using System.Text;
using ES.FX.Zendesk.Tests.Testing;
using ES.FX.Zendesk.Uploads;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Uploads;

public class ZendeskUploadsApiTests
{
    private static ZendeskUploadsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskUploadsApi>.Instance);

    [Fact]
    public async Task UploadAsync_Posts_Raw_Bytes_With_Content_Type_And_Parses_Token()
    {
        var stub = new StubHttpMessageHandler(
            """{ "upload": { "token": "tok123", "attachment": { "id": 5, "file_name": "log.txt" }, "attachments": [ { "id": 5 } ] } }""");
        var api = CreateApi(stub);
        var bytes = Encoding.UTF8.GetBytes("log line 1");

        var upload = await api.UploadAsync("log.txt", bytes, "text/plain",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("tok123", upload.Token);
        Assert.Equal("log.txt", upload.Attachment?.FileName);
        Assert.Equal(HttpMethod.Post, stub.LastRequest!.Method);
        Assert.Equal("/api/v2/uploads.json", stub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("filename=log.txt", stub.LastRequest.RequestUri.Query);
        Assert.Equal("text/plain", stub.LastRequest.Content?.Headers.ContentType?.MediaType);
        Assert.Equal("log line 1", stub.LastRequestBody); // RAW body — not JSON, not multipart
    }

    [Fact]
    public async Task UploadAsync_Chains_Multi_File_Uploads_Via_Token()
    {
        var stub = new StubHttpMessageHandler("""{ "upload": { "token": "tok123", "attachments": [] } }""");
        var api = CreateApi(stub);

        await api.UploadAsync("b.txt", "b"u8.ToArray(), "text/plain", "tok123",
            TestContext.Current.CancellationToken);

        Assert.Contains("token=tok123", stub.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task UploadAsync_Accepts_Parameterized_Content_Types()
    {
        // "text/plain; charset=utf-8" must parse — the MediaTypeHeaderValue constructor would throw on it.
        var stub = new StubHttpMessageHandler("""{ "upload": { "token": "tok1" } }""");
        var api = CreateApi(stub);

        var upload = await api.UploadAsync("log.txt", "x"u8.ToArray(), "text/plain; charset=utf-8",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("tok1", upload.Token);
        Assert.Equal("text/plain", stub.LastRequest!.Content?.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", stub.LastRequest.Content?.Headers.ContentType?.CharSet);
    }

    [Fact]
    public async Task UploadAsync_Validates_FileName_And_ContentType()
    {
        var api = CreateApi(new StubHttpMessageHandler("{}"));

        await Assert.ThrowsAsync<ArgumentException>(() => api.UploadAsync(" ", "x"u8.ToArray(), "text/plain",
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => api.UploadAsync("a.txt", "x"u8.ToArray(), " ",
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_Escapes_The_Token()
    {
        var stub = new StubHttpMessageHandler("");
        var api = CreateApi(stub);

        await api.DeleteAsync("tok/123", TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, stub.LastRequest!.Method);
        Assert.Contains("uploads/tok%2F123.json", stub.LastRequest.RequestUri!.AbsoluteUri);
    }
}