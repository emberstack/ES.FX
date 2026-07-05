using System.Net;
using ES.FX.Zendesk.Attachments;
using ES.FX.Zendesk.Authentication;

namespace ES.FX.Zendesk.Tests.Attachments;

public class ZendeskAttachmentContentFetcherTests
{
    private const int MaxContentBytes = 1024 * 1024;
    private const string ZendeskContentUrl = "https://acme.zendesk.com/attachments/token/abc/?name=log.txt";

    /// <summary>
    ///     Builds the fetcher over the REAL auth delegating handler (stub token provider) so the tests observe
    ///     exactly which requests carry the Authorization header — the same chain the DI registration wires up.
    /// </summary>
    private static (ZendeskAttachmentContentFetcher Fetcher, ContentHandler Handler) CreateFetcher(
        byte[] content, string baseAddress = "https://acme.zendesk.com/api/v2/")
    {
        var handler = new ContentHandler(content);
        var auth = new ZendeskAuthenticationDelegatingHandler(new StubTokenProvider()) { InnerHandler = handler };
        var client = new HttpClient(auth) { BaseAddress = new Uri(baseAddress) };
        return (new ZendeskAttachmentContentFetcher(client), handler);
    }

    [Fact]
    public async Task DownloadAsync_Returns_Text_Decoded_As_Utf8()
    {
        var (fetcher, handler) = CreateFetcher("hello logs"u8.ToArray());

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("utf-8", content.Encoding);
        Assert.Equal("hello logs", content.Content);
        Assert.False(content.Truncated);
        Assert.Equal("/attachments/token/abc/", handler.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DownloadAsync_Honors_A_Caller_Supplied_Cap()
    {
        var (fetcher, _) = CreateFetcher("hello logs"u8.ToArray());

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain", 5,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(content.Truncated);
        Assert.Equal("hello", content.Content); // exactly the caller's cap

        // A cap above the payload size downloads everything untruncated.
        var (bigFetcher, _) = CreateFetcher("hello logs"u8.ToArray());
        var full = await bigFetcher.DownloadAsync(ZendeskContentUrl, "text/plain", 8 * 1024 * 1024,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(full.Truncated);
        Assert.Equal("hello logs", full.Content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DownloadAsync_Rejects_A_NonPositive_Cap(int cap)
    {
        var (fetcher, _) = CreateFetcher("x"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fetcher.DownloadAsync(ZendeskContentUrl, "text/plain", cap,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_Returns_Binary_As_Base64()
    {
        var bytes = new byte[] { 1, 2, 3, 250, 251 };
        var (fetcher, _) = CreateFetcher(bytes);

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "image/png",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("base64", content.Encoding);
        Assert.Equal(Convert.ToBase64String(bytes), content.Content);
    }

    [Fact]
    public async Task DownloadAsync_Default_Is_Unlimited()
    {
        var bytes = new byte[MaxContentBytes + 100];
        Array.Fill(bytes, (byte)'A');
        var (fetcher, _) = CreateFetcher(bytes);

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(content.Truncated); // no cap by default — the whole payload comes back
        Assert.Equal(MaxContentBytes + 100, content.Content.Length);
    }

    [Fact]
    public async Task DownloadAsync_Exactly_At_Cap_Is_Not_Truncated()
    {
        var bytes = new byte[MaxContentBytes];
        Array.Fill(bytes, (byte)'A');
        var (fetcher, _) = CreateFetcher(bytes);

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain", MaxContentBytes,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(content.Truncated); // complete payload — the flag must not lie
        Assert.Equal(MaxContentBytes, content.Content.Length);
    }

    [Fact]
    public async Task DownloadAsync_One_Byte_Over_Cap_Is_Truncated_To_Cap()
    {
        var bytes = new byte[MaxContentBytes + 1];
        Array.Fill(bytes, (byte)'A');
        var (fetcher, _) = CreateFetcher(bytes);

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain", MaxContentBytes,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(content.Truncated);
        Assert.Equal(MaxContentBytes, content.Content.Length);
    }

    [Fact]
    public async Task DownloadAsync_Truncation_Does_Not_Split_A_MultiByte_Utf8_Character()
    {
        // (cap - 1) ASCII bytes followed by '€' (E2 82 AC): the cap cuts after the lead byte E2. The decoded
        // text must end cleanly at the last complete character — no U+FFFD replacement garbage.
        var euro = "€"u8.ToArray();
        var bytes = new byte[MaxContentBytes - 1 + euro.Length];
        Array.Fill(bytes, (byte)'A', 0, MaxContentBytes - 1);
        euro.CopyTo(bytes, MaxContentBytes - 1);
        var (fetcher, _) = CreateFetcher(bytes);

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain", MaxContentBytes,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(content.Truncated);
        Assert.DoesNotContain('�', content.Content);
        Assert.Equal(MaxContentBytes - 1, content.Content.Length); // the partial '€' was dropped, not mangled
    }

    [Fact]
    public async Task DownloadAsync_Offset_Skips_Bytes_Before_Decoding()
    {
        var (fetcher, _) = CreateFetcher("hello logs"u8.ToArray());

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain", offset: 6,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("logs", content.Content);
        Assert.Equal(4, content.ReturnedBytes);
        Assert.False(content.Truncated);
    }

    [Fact]
    public async Task DownloadAsync_Offset_Composes_With_The_Cap_For_Ranged_Continuation()
    {
        var (fetcher, _) = CreateFetcher("abcdefghij"u8.ToArray());

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain", 3, 2,
            TestContext.Current.CancellationToken);

        Assert.Equal("cde", content.Content); // bytes [2, 5) — offset applied before the cap
        Assert.Equal(3, content.ReturnedBytes);
        Assert.True(content.Truncated); // more payload remains beyond offset + cap
    }

    [Theory]
    [InlineData(3)] // exactly at the end
    [InlineData(10)] // past the end
    public async Task DownloadAsync_Offset_At_Or_Past_The_End_Returns_Empty_Untruncated(long offset)
    {
        var (fetcher, _) = CreateFetcher("abc"u8.ToArray());

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain", offset: offset,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, content.Content);
        Assert.Equal(0, content.ReturnedBytes);
        Assert.False(content.Truncated);
    }

    [Fact]
    public async Task DownloadAsync_Rejects_A_Negative_Offset()
    {
        var (fetcher, _) = CreateFetcher("abc"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fetcher.DownloadAsync(ZendeskContentUrl, "text/plain", offset: -1,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_ReturnedBytes_Reflects_The_Utf8_Tail_Trim_So_Continuation_Stays_Clean()
    {
        // "ab€": the cap cuts after the '€' lead byte; the trim drops it, so ReturnedBytes must be 2 — and
        // continuing from offset + ReturnedBytes must decode the complete '€' with nothing lost or doubled.
        var payload = "ab€"u8.ToArray();
        var (fetcher, _) = CreateFetcher(payload);
        var first = await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain", 3,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("ab", first.Content);
        Assert.Equal(2, first.ReturnedBytes);
        Assert.True(first.Truncated);

        var (continuation, _) = CreateFetcher(payload);
        var second = await continuation.DownloadAsync(ZendeskContentUrl, "text/plain",
            offset: first.ReturnedBytes, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("€", second.Content);
        Assert.Equal(3, second.ReturnedBytes);
        Assert.False(second.Truncated);
    }

    [Fact]
    public async Task DownloadAsync_ReturnedBytes_Counts_Raw_Bytes_For_Base64_Payloads()
    {
        var bytes = new byte[] { 1, 2, 3, 250, 251 };
        var (fetcher, _) = CreateFetcher(bytes);

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "image/png",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("base64", content.Encoding);
        Assert.Equal(5, content.ReturnedBytes); // the raw payload size, not the base64 string length
    }

    [Fact]
    public async Task DownloadAsync_Honors_A_Supported_Declared_Charset()
    {
        // 'café' in ISO-8859-1: the 0xE9 byte is invalid UTF-8 and would mojibake without charset handling.
        var bytes = new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 };
        var (fetcher, _) = CreateFetcher(bytes);

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/csv; charset=iso-8859-1",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("utf-8", content.Encoding);
        Assert.Equal("café", content.Content);
    }

    [Fact]
    public async Task DownloadAsync_Falls_Back_To_Base64_For_An_Unknown_Charset()
    {
        // An undecodable declared charset must NOT be lossily forced through UTF-8 — return the raw bytes.
        var bytes = new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 };
        var (fetcher, _) = CreateFetcher(bytes);

        var content = await fetcher.DownloadAsync(ZendeskContentUrl, "text/csv; charset=not-a-charset",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("base64", content.Encoding);
        Assert.Equal(Convert.ToBase64String(bytes), content.Content);
    }

    [Fact]
    public async Task DownloadAsync_Sends_Bearer_To_The_Configured_Zendesk_Host()
    {
        var (fetcher, handler) = CreateFetcher("x"u8.ToArray());

        await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Bearer", handler.AuthScheme); // tenant host — credentials expected
        Assert.False(handler.SkipAuthenticationOptionSet);
    }

    [Fact]
    public async Task DownloadAsync_Does_Not_Send_Credentials_To_An_External_Content_Host()
    {
        // Zendesk documents that content_url can point to externally hosted files and instructs clients to
        // check the domain before sending credentials. The tenant's Bearer token must stay home.
        var (fetcher, handler) = CreateFetcher("x"u8.ToArray());

        var content = await fetcher.DownloadAsync("https://cdn.example.com/files/log.txt", "text/plain",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("x", content.Content);
        Assert.Null(handler.AuthScheme); // external host gets NO credentials
        Assert.True(handler.SkipAuthenticationOptionSet); // suppressed via the per-request option
    }

    [Fact]
    public async Task DownloadAsync_Refuses_A_NonHttps_External_Content_Url()
    {
        var (fetcher, _) = CreateFetcher("x"u8.ToArray());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fetcher.DownloadAsync("http://cdn.example.com/files/log.txt", "text/plain",
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_Does_Not_Send_Credentials_To_Another_Zendesk_Tenant()
    {
        // Only the CONFIGURED host is trusted. A content_url pointing at a different *.zendesk.com
        // subdomain is another tenant — it must be fetched anonymously, like any external host.
        var (fetcher, handler) = CreateFetcher("x"u8.ToArray());

        var content = await fetcher.DownloadAsync(
            "https://other-tenant.zendesk.com/attachments/token/abc/?name=log.txt", "text/plain",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("x", content.Content);
        Assert.Null(handler.AuthScheme); // foreign tenant gets NO credentials
    }

    [Fact]
    public async Task DownloadAsync_Refuses_A_Plain_Http_Url_Even_On_The_Configured_Host()
    {
        // Same host but downgraded to http: the bearer token must never travel in cleartext, and an
        // untrusted non-https URL is refused outright.
        var (fetcher, _) = CreateFetcher("x"u8.ToArray());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fetcher.DownloadAsync("http://acme.zendesk.com/attachments/token/abc/?name=log.txt",
                "text/plain", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_Trusts_A_Matching_Http_Host_When_The_Configured_BaseAddress_Is_Http()
    {
        // An http BaseUrl test double stays usable: same host, same (http) scheme as configured → trusted.
        var (fetcher, handler) = CreateFetcher("x"u8.ToArray(), "http://localhost:5000/api/v2/");

        var content = await fetcher.DownloadAsync("http://localhost:5000/attachments/token/abc/", "text/plain",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("x", content.Content);
        Assert.Equal("Bearer", handler.AuthScheme);
    }

    [Fact]
    public async Task DownloadAsync_Rejects_A_Relative_Content_Url()
    {
        var (fetcher, _) = CreateFetcher("x"u8.ToArray());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fetcher.DownloadAsync("attachments/token/abc/", "text/plain",
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_Throws_ZendeskApiException_When_The_Download_Fails()
    {
        // e.g. an expired S3 token: the error body must NOT be base64'd and returned as file content.
        var (fetcher, handler) = CreateFetcher("x"u8.ToArray());
        handler.Status = HttpStatusCode.Forbidden;

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(async () =>
            await fetcher.DownloadAsync(ZendeskContentUrl, "text/plain",
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
    }

    private sealed class StubTokenProvider : IZendeskAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(bool forceRefresh = false,
            CancellationToken cancellationToken = default) => Task.FromResult("tok-1");
    }

    /// <summary>
    ///     Serves the content bytes for any request, recording the Authorization scheme the request carried and
    ///     whether the <see cref="ZendeskAuthenticationDelegatingHandler.SkipAuthentication" /> option was set.
    /// </summary>
    private sealed class ContentHandler(byte[] content) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public Uri? RequestUri { get; private set; }
        public string? AuthScheme { get; private set; }
        public bool SkipAuthenticationOptionSet { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            AuthScheme = request.Headers.Authorization?.Scheme;
            SkipAuthenticationOptionSet =
                request.Options.TryGetValue(ZendeskAuthenticationDelegatingHandler.SkipAuthentication,
                    out var skip) && skip;
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }
}