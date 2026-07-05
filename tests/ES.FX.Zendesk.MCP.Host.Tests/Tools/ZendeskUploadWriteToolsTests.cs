using System.Net;
using System.Text;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskUploadWriteToolsTests
{
    private static (ZendeskUploadWriteTools Tools, ZendeskToolHarness Harness) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var harness = new ZendeskToolHarness();
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return (new ZendeskUploadWriteTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            executionMode.Object), harness);
    }

    [Fact]
    public async Task Create_Sends_Raw_Bytes_With_Filename_And_Token_And_Returns_The_Lean_Confirmation()
    {
        var (tools, harness) = Create();
        // A chained (multi-file) upload: 'attachments' carries every file on the token so far, and the
        // top-level 'attachment' duplicates the last element.
        harness.EnqueueJson("""
                            {"upload":{"token":"tok-1",
                             "attachment":{"id":5,"file_name":"report.png","content_type":"image/png","size":12,
                              "content_url":"https://unit-test.zendesk.com/attachments/token/x/?name=report.png",
                              "url":"https://unit-test.zendesk.com/api/v2/attachments/5.json"},
                             "attachments":[
                              {"id":4,"file_name":"first.txt","content_type":"text/plain","size":3,
                               "content_url":"https://unit-test.zendesk.com/attachments/token/x/?name=first.txt",
                               "url":"https://unit-test.zendesk.com/api/v2/attachments/4.json",
                               "thumbnails":[{"id":40}]},
                              {"id":5,"file_name":"report.png","content_type":"image/png","size":12,
                               "content_url":"https://unit-test.zendesk.com/attachments/token/x/?name=report.png",
                               "url":"https://unit-test.zendesk.com/api/v2/attachments/5.json"}]}}
                            """);

        var result = await tools.Create("report.png", Convert.ToBase64String(Encoding.UTF8.GetBytes("file-content")),
            "image/png", "existing-token", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/uploads", request.Path);
        Assert.Contains("filename=report.png", request.Query);
        Assert.Contains("token=existing-token", request.Query);
        // The raw file bytes (not JSON, not multipart), typed by the caller's content type — Zendesk types the
        // attachment from the Content-Type header.
        Assert.Equal("file-content", request.Body);
        Assert.Equal("image/png", request.ContentType);

        // The lean confirmation: {token, attachments summary rows}. The duplicate top-level 'attachment' (the
        // last element of 'attachments') is dropped, and the envelope wrapper never surfaces.
        var element = Assert.IsType<JsonElement>(result);
        Assert.False(element.TryGetProperty("upload", out _));
        Assert.False(element.TryGetProperty("attachment", out _));
        Assert.Equal("tok-1", element.GetProperty("token").GetString());
        var attachments = element.GetProperty("attachments");
        Assert.Equal(2, attachments.GetArrayLength()); // every file on the token so far, not just this call's
        Assert.Equal(4, attachments[0].GetProperty("id").GetInt64());
        Assert.Equal("first.txt", attachments[0].GetProperty("file_name").GetString());
        Assert.Equal("image/png", attachments[1].GetProperty("content_type").GetString());
        Assert.Equal(12, attachments[1].GetProperty("size").GetInt64());
        // Summary rows are allowlisted — links and thumbnails do not appear.
        Assert.False(attachments[0].TryGetProperty("content_url", out _));
        Assert.False(attachments[0].TryGetProperty("url", out _));
        Assert.False(attachments[0].TryGetProperty("thumbnails", out _));
    }

    [Fact]
    public async Task Create_Omits_Token_Query_When_Not_Chaining()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"upload":{"token":"tok-1"}}""");

        var result = await tools.Create("report.png", Convert.ToBase64String(Encoding.UTF8.GetBytes("file-content")),
            "image/png", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("filename=report.png", harness.Request.Query);
        Assert.DoesNotContain("token=", harness.Request.Query);
        // 'attachments' is always present in the confirmation — an empty array, not an absent key.
        var element = Assert.IsType<JsonElement>(result);
        Assert.Equal("tok-1", element.GetProperty("token").GetString());
        Assert.Equal(0, element.GetProperty("attachments").GetArrayLength());
    }

    [Fact]
    public async Task Create_Invalid_Base64_Throws_McpException_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Create("report.png", "%%%not-base64%%%", "image/png",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("The 'contentBase64' parameter is not valid base64-encoded content.", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Create_Without_Upload_In_Response_Throws_An_Actionable_McpException()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        // A 2xx with no 'upload' token now surfaces as an McpException (actionable to the agent) rather than an
        // opaque InvalidOperationException, and the message tells the agent the file may already exist server-side.
        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Create("report.png", Convert.ToBase64String(Encoding.UTF8.GetBytes("file-content")),
                "image/png", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("returned no 'upload' token", exception.Message);
        Assert.Single(harness.Requests);
    }

    [Fact]
    public async Task Delete_Returns_Acknowledgement()
    {
        var (tools, harness) = Create();
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.Delete("tok-1", TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete upload token 'tok-1'", acknowledgement.Description);
        Assert.Equal(HttpMethod.Delete, harness.Request.Method);
        Assert.Equal("/api/v2/uploads/tok-1", harness.Request.Path);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create("report.png", Convert.ToBase64String([1, 2, 3, 4]), "image/png",
            cancellationToken: TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("upload file 'report.png'", dryRun.Description);

        // The echo omits the base64 payload (frozen convention) — only its decoded length is reported.
        Assert.NotNull(dryRun.Request);
        var echo = JsonSerializer.Serialize(dryRun.Request);
        Assert.Contains("\"fileName\":\"report.png\"", echo);
        Assert.Contains("\"contentType\":\"image/png\"", echo);
        Assert.Contains("\"contentLength\":4", echo);
        Assert.DoesNotContain("contentBase64", echo);

        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete("tok-1", TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}