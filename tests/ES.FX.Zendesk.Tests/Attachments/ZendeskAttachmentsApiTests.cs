using System.Net;
using System.Text;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Attachments;
using ES.FX.Zendesk.Authentication;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Attachments;

public class ZendeskAttachmentsApiTests
{
    private const int MaxContentBytes = 1024 * 1024;
    private const string ZendeskContentUrl = "https://acme.zendesk.com/attachments/token/abc/?name=log.txt";

    /// <summary>
    ///     Builds the API over the REAL auth delegating handler (stub token provider) so the tests observe
    ///     exactly which requests carry the Authorization header.
    /// </summary>
    private static (ZendeskAttachmentsApi Api, AttachmentHandler Handler) CreateApi(
        string contentType, byte[] content, string contentUrl = ZendeskContentUrl)
    {
        var handler = new AttachmentHandler(contentType, content, contentUrl);
        var auth = new ZendeskAuthenticationDelegatingHandler(new StubTokenProvider()) { InnerHandler = handler };
        var client = new HttpClient(auth) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") };
        return (new ZendeskAttachmentsApi(client, NullLogger<ZendeskAttachmentsApi>.Instance), handler);
    }

    [Fact]
    public async Task GetContentAsync_Returns_Text_Decoded_As_Utf8()
    {
        var (api, handler) = CreateApi("text/plain", "hello logs"u8.ToArray());

        var content = await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("log.txt", content.FileName);
        Assert.Equal("utf-8", content.Encoding);
        Assert.Equal("hello logs", content.Content);
        Assert.False(content.Truncated);
        Assert.Equal("/api/v2/attachments/88.json", handler.MetadataPath);
    }

    [Fact]
    public async Task GetContentAsync_Honors_A_Caller_Supplied_Cap()
    {
        var (api, _) = CreateApi("text/plain", "hello logs"u8.ToArray());

        var content = await api.GetContentAsync(88, maxContentBytes: 5,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(content.Truncated);
        Assert.Equal("hello", content.Content); // exactly the caller's cap

        // A cap above the payload size downloads everything untruncated.
        var (bigApi, _) = CreateApi("text/plain", "hello logs"u8.ToArray());
        var full = await bigApi.GetContentAsync(88, maxContentBytes: 8 * 1024 * 1024,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(full.Truncated);
        Assert.Equal("hello logs", full.Content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetContentAsync_Rejects_A_NonPositive_Cap(int cap)
    {
        var (api, _) = CreateApi("text/plain", "x"u8.ToArray());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            api.GetContentAsync(88, cap, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetContentAsync_Returns_Binary_As_Base64()
    {
        var bytes = new byte[] { 1, 2, 3, 250, 251 };
        var (api, _) = CreateApi("image/png", bytes);

        var content = await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("base64", content.Encoding);
        Assert.Equal(Convert.ToBase64String(bytes), content.Content);
    }

    [Fact]
    public async Task GetContentAsync_Default_Is_Unlimited()
    {
        var bytes = new byte[MaxContentBytes + 100];
        Array.Fill(bytes, (byte)'A');
        var (api, _) = CreateApi("text/plain", bytes);

        var content = await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(content.Truncated); // no cap by default — the whole payload comes back
        Assert.Equal(MaxContentBytes + 100, content.Content.Length);
    }

    [Fact]
    public async Task GetContentAsync_Exactly_At_Cap_Is_Not_Truncated()
    {
        var bytes = new byte[MaxContentBytes];
        Array.Fill(bytes, (byte)'A');
        var (api, _) = CreateApi("text/plain", bytes);

        var content = await api.GetContentAsync(88, MaxContentBytes, TestContext.Current.CancellationToken);

        Assert.False(content.Truncated); // complete payload — the flag must not lie
        Assert.Equal(MaxContentBytes, content.Content.Length);
    }

    [Fact]
    public async Task GetContentAsync_One_Byte_Over_Cap_Is_Truncated_To_Cap()
    {
        var bytes = new byte[MaxContentBytes + 1];
        Array.Fill(bytes, (byte)'A');
        var (api, _) = CreateApi("text/plain", bytes);

        var content = await api.GetContentAsync(88, MaxContentBytes, TestContext.Current.CancellationToken);

        Assert.True(content.Truncated);
        Assert.Equal(MaxContentBytes, content.Content.Length);
    }

    [Fact]
    public async Task GetContentAsync_Truncation_Does_Not_Split_A_MultiByte_Utf8_Character()
    {
        // (cap - 1) ASCII bytes followed by '€' (E2 82 AC): the cap cuts after the lead byte E2. The decoded
        // text must end cleanly at the last complete character — no U+FFFD replacement garbage.
        var euro = "€"u8.ToArray();
        var bytes = new byte[MaxContentBytes - 1 + euro.Length];
        Array.Fill(bytes, (byte)'A', 0, MaxContentBytes - 1);
        euro.CopyTo(bytes, MaxContentBytes - 1);
        var (api, _) = CreateApi("text/plain", bytes);

        var content = await api.GetContentAsync(88, MaxContentBytes, TestContext.Current.CancellationToken);

        Assert.True(content.Truncated);
        Assert.DoesNotContain('�', content.Content);
        Assert.Equal(MaxContentBytes - 1, content.Content.Length); // the partial '€' was dropped, not mangled
    }

    [Fact]
    public async Task GetContentAsync_Honors_A_Supported_Declared_Charset()
    {
        // 'café' in ISO-8859-1: the 0xE9 byte is invalid UTF-8 and would mojibake without charset handling.
        var bytes = new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 };
        var (api, _) = CreateApi("text/csv; charset=iso-8859-1", bytes);

        var content = await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("utf-8", content.Encoding);
        Assert.Equal("café", content.Content);
    }

    [Fact]
    public async Task GetContentAsync_Falls_Back_To_Base64_For_An_Unknown_Charset()
    {
        // An undecodable declared charset must NOT be lossily forced through UTF-8 — return the raw bytes.
        var bytes = new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE9 };
        var (api, _) = CreateApi("text/csv; charset=not-a-charset", bytes);

        var content = await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("base64", content.Encoding);
        Assert.Equal(Convert.ToBase64String(bytes), content.Content);
    }

    [Fact]
    public async Task GetContentAsync_Sends_Bearer_To_The_Zendesk_Content_Host()
    {
        var (api, handler) = CreateApi("text/plain", "x"u8.ToArray());

        await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Bearer", handler.MetadataAuthScheme);
        Assert.Equal("Bearer", handler.ContentAuthScheme); // tenant host — credentials expected
    }

    [Fact]
    public async Task GetContentAsync_Does_Not_Send_Credentials_To_An_External_Content_Host()
    {
        // Zendesk documents that content_url can point to externally hosted files and instructs clients to
        // check the domain before sending credentials. The tenant's Bearer token must stay home.
        var (api, handler) = CreateApi("text/plain", "x"u8.ToArray(),
            "https://cdn.example.com/files/log.txt");

        var content = await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("x", content.Content);
        Assert.Equal("Bearer", handler.MetadataAuthScheme); // API call stays authenticated
        Assert.Null(handler.ContentAuthScheme); // external host gets NO credentials
    }

    [Fact]
    public async Task GetContentAsync_Refuses_A_NonHttps_External_Content_Url()
    {
        var (api, _) = CreateApi("text/plain", "x"u8.ToArray(),
            "http://cdn.example.com/files/log.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetContentAsync_Does_Not_Send_Credentials_To_Another_Zendesk_Tenant()
    {
        // Only the CONFIGURED host is trusted. A content_url pointing at a different *.zendesk.com
        // subdomain is another tenant — it must be fetched anonymously, like any external host.
        var (api, handler) = CreateApi("text/plain", "x"u8.ToArray(),
            "https://other-tenant.zendesk.com/attachments/token/abc/?name=log.txt");

        var content = await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("x", content.Content);
        Assert.Equal("Bearer", handler.MetadataAuthScheme);
        Assert.Null(handler.ContentAuthScheme); // foreign tenant gets NO credentials
    }

    [Fact]
    public async Task GetContentAsync_Refuses_A_Plain_Http_Url_Even_On_The_Configured_Host()
    {
        // Same host but downgraded to http: the bearer token must never travel in cleartext, and an
        // untrusted non-https URL is refused outright.
        var (api, _) = CreateApi("text/plain", "x"u8.ToArray(),
            "http://acme.zendesk.com/attachments/token/abc/?name=log.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetContentAsync_Throws_On_Empty_Metadata_Envelope()
    {
        var (api, handler) = CreateApi("text/plain", "x"u8.ToArray());
        handler.MetadataJson = "{}";

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetContentAsync_Throws_When_ContentUrl_Is_Missing()
    {
        var (api, handler) = CreateApi("text/plain", "x"u8.ToArray());
        handler.MetadataJson = """{ "attachment": { "id": 88, "file_name": "log.txt" } }""";

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetContentAsync_Throws_ZendeskApiException_When_The_Content_Download_Fails()
    {
        // e.g. an expired S3 token: the error body must NOT be base64'd and returned as file content.
        var (api, handler) = CreateApi("text/plain", "x"u8.ToArray());
        handler.ContentStatus = HttpStatusCode.Forbidden;

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(async () =>
            await api.GetContentAsync(88, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
    }

    private sealed class StubTokenProvider : IZendeskAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(bool forceRefresh = false,
            CancellationToken cancellationToken = default) => Task.FromResult("tok-1");
    }

    /// <summary>
    ///     Routes <c>/attachments/{id}.json</c> to attachment metadata (with a configurable content URL) and any
    ///     other request to the content bytes, recording the Authorization scheme each request carried.
    /// </summary>
    private sealed class AttachmentHandler(string contentType, byte[] content, string contentUrl) : HttpMessageHandler
    {
        public string? MetadataJson { get; set; }
        public HttpStatusCode ContentStatus { get; set; } = HttpStatusCode.OK;
        public string? MetadataPath { get; private set; }
        public string? MetadataAuthScheme { get; private set; }
        public string? ContentAuthScheme { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/attachments/88.json", StringComparison.Ordinal))
            {
                MetadataPath = path;
                MetadataAuthScheme = request.Headers.Authorization?.Scheme;
                var json = MetadataJson ??
                           $$"""
                             { "attachment": { "id": 88, "file_name": "log.txt", "content_type": "{{contentType}}", "size": {{content.Length}}, "content_url": "{{contentUrl}}" } }
                             """;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }

            ContentAuthScheme = request.Headers.Authorization?.Scheme;
            return Task.FromResult(new HttpResponseMessage(ContentStatus)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }
}