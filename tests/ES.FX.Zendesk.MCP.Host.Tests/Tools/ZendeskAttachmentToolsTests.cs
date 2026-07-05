using System.Net;
using System.Text;
using ES.FX.Zendesk.Attachments;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskAttachmentToolsTests
{
    private const string ContentUrl = ZendeskToolHarness.BaseUrl + "/attachments/token/abc123/?name=log.txt";

    private static (ZendeskAttachmentTools Tools, ZendeskToolHarness Harness) Create()
    {
        var harness = new ZendeskToolHarness();
        // The fetcher gets its own HttpClient over the SAME harness (mirroring production DI, where it shares
        // the Zendesk handler chain), so metadata and content requests land in one Requests list in call order.
        var contentFetcher = new ZendeskAttachmentContentFetcher(new HttpClient(harness, false)
        {
            BaseAddress = new Uri(ZendeskToolHarness.BaseUrl)
        });
        return (new ZendeskAttachmentTools(harness.CreateSupportClient(), contentFetcher), harness);
    }

    [Fact]
    public async Task Read_Fetches_Metadata_Then_Downloads_Text_Content()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson($$$"""
                               {"attachment":{"id":88,"file_name":"log.txt","content_type":"text/plain","size":11,
                                "content_url":"{{{ContentUrl}}}"}}
                               """);
        harness.EnqueueStatus(HttpStatusCode.OK, "hello world");

        var result = await tools.Read(88, TestContext.Current.CancellationToken);

        Assert.Equal(2, harness.Requests.Count);
        Assert.Equal(HttpMethod.Get, harness.Requests[0].Method);
        Assert.Equal("/api/v2/attachments/88", harness.Requests[0].Path);
        Assert.Equal(ContentUrl, harness.Requests[1].Uri.ToString());

        Assert.Equal(88, result.Id);
        Assert.Equal("log.txt", result.FileName);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal(11, result.Size);
        Assert.Equal("utf-8", result.Encoding);
        Assert.Equal("hello world", result.Content);
        // The ranged-download bookkeeping: whole payload returned, from the start, nothing cut.
        Assert.Equal(11, result.ReturnedBytes);
        Assert.Equal(0L, result.Offset);
        Assert.False(result.Truncated);
        Assert.Null(result.Note);
    }

    [Fact]
    public async Task Read_Returns_Binary_Content_As_Base64()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson($$$"""
                               {"attachment":{"id":88,"file_name":"pixel.png","content_type":"image/png","size":7,
                                "content_url":"{{{ContentUrl}}}"}}
                               """);
        harness.EnqueueStatus(HttpStatusCode.OK, "PNGDATA");

        var result = await tools.Read(88, TestContext.Current.CancellationToken);

        Assert.Equal("base64", result.Encoding);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("PNGDATA")), result.Content);
        // returned_bytes counts RAW payload bytes, not base64 characters.
        Assert.Equal(7, result.ReturnedBytes);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task Read_Caps_Content_At_The_Default_MaxBytes_And_Names_The_Continuation()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson($$$"""
                               {"attachment":{"id":88,"file_name":"big.txt","content_type":"text/plain",
                                "content_url":"{{{ContentUrl}}}"}}
                               """);
        // Larger than the default 32 KiB cap — the tool must cut the payload and explain how to continue.
        harness.EnqueueStatus(HttpStatusCode.OK, new string('a', 32 * 1024 + 5));

        var result = await tools.Read(88, TestContext.Current.CancellationToken);

        Assert.True(result.Truncated);
        Assert.Equal(32 * 1024, result.Content.Length);
        Assert.Equal(32 * 1024, result.ReturnedBytes);
        Assert.Equal(0L, result.Offset);
        // The note names the exact ranged re-call: offset = this call's offset + returned_bytes.
        Assert.Contains("maxBytes:32768", result.Note);
        Assert.Contains("offset:32768", result.Note);
    }

    [Fact]
    public async Task Read_Skips_The_Offset_Before_Decoding()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson($$$"""
                               {"attachment":{"id":88,"file_name":"log.txt","content_type":"text/plain","size":11,
                                "content_url":"{{{ContentUrl}}}"}}
                               """);
        harness.EnqueueStatus(HttpStatusCode.OK, "hello world");

        var result = await tools.Read(88, TestContext.Current.CancellationToken, offset: 6);

        // The offset is a client-side skip-read (content_url does not reliably honor HTTP Range) — the
        // download request itself is unchanged.
        Assert.Equal(ContentUrl, harness.Requests[1].Uri.ToString());
        Assert.Equal("world", result.Content);
        Assert.Equal(5, result.ReturnedBytes);
        Assert.Equal(6L, result.Offset);
        Assert.False(result.Truncated);
        Assert.Null(result.Note);
    }

    [Fact]
    public async Task Read_Truncated_At_An_Offset_Names_The_Absolute_Continuation_Offset()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson($$$"""
                               {"attachment":{"id":88,"file_name":"log.txt","content_type":"text/plain","size":10,
                                "content_url":"{{{ContentUrl}}}"}}
                               """);
        harness.EnqueueStatus(HttpStatusCode.OK, "abcdefghij");

        var result = await tools.Read(88, TestContext.Current.CancellationToken, 4, 2);

        Assert.Equal("cdef", result.Content);
        Assert.True(result.Truncated);
        Assert.Equal(4, result.ReturnedBytes);
        Assert.Equal(2L, result.Offset);
        // The continuation offset is ABSOLUTE (this call's offset + returned_bytes), not per-call relative.
        Assert.Contains("maxBytes:4", result.Note);
        Assert.Contains("offset:6", result.Note);
    }

    [Fact]
    public async Task Read_Offset_Past_The_End_Returns_An_Empty_Untruncated_Result()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson($$$"""
                               {"attachment":{"id":88,"file_name":"log.txt","content_type":"text/plain","size":11,
                                "content_url":"{{{ContentUrl}}}"}}
                               """);
        harness.EnqueueStatus(HttpStatusCode.OK, "hello world");

        var result = await tools.Read(88, TestContext.Current.CancellationToken, offset: 100);

        // The natural end of a chained ranged download: nothing left, and no misleading truncation flag.
        Assert.Equal(string.Empty, result.Content);
        Assert.Equal(0, result.ReturnedBytes);
        Assert.False(result.Truncated);
        Assert.Null(result.Note);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(64 * 1024 + 1)]
    public async Task Read_Rejects_An_Invalid_MaxBytes_Without_Calling_Zendesk(int maxBytes)
    {
        // The 64 KiB hard cap keeps the base64 of a single call (~22k tokens) under the client response cap —
        // larger reads must page with offset, so the rejection names that path.
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(88, TestContext.Current.CancellationToken, maxBytes));

        Assert.Contains("65536", exception.Message);
        Assert.Contains("offset", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Rejects_A_Negative_Offset_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(88, TestContext.Current.CancellationToken, offset: -1));

        Assert.Contains("offset", exception.Message);
        Assert.Contains("returned_bytes", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Missing_Attachment_Throws_With_Legacy_Message()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.Read(88, TestContext.Current.CancellationToken));

        Assert.Equal("Zendesk attachment '88' was not found.", exception.Message);
        Assert.Single(harness.Requests); // no content download was attempted
    }

    [Fact]
    public async Task Read_Missing_Content_Url_Throws_With_Legacy_Message()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"attachment":{"id":88,"file_name":"log.txt"}}""");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tools.Read(88, TestContext.Current.CancellationToken));

        Assert.Equal("Zendesk attachment '88' has no downloadable content URL.", exception.Message);
        Assert.Single(harness.Requests);
    }
}