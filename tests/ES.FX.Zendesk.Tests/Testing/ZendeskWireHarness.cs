using System.Net;
using System.Text;
using ES.FX.Zendesk.HelpCenter;
using ES.FX.Zendesk.Support;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace ES.FX.Zendesk.Tests.Testing;

/// <summary>
///     Wire-level harness for generated-client tests: builds real generated clients over a stub handler, records
///     every outgoing request (method, absolute URI, body) and replays canned JSON responses — so tests assert
///     the actual request Zendesk would receive (paths, percent-encoded query parameters) instead of mocking
///     typed interfaces that could never see the wire.
/// </summary>
internal sealed class ZendeskWireHarness : HttpMessageHandler
{
    public const string BaseUrl = "https://unit-test.zendesk.com";

    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<CapturedRequest> Requests { get; } = [];

    /// <summary>The single captured request — for the common one-call test.</summary>
    public CapturedRequest Request => Assert.Single(Requests);

    public void EnqueueJson(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        _responses.Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    public void EnqueueStatus(HttpStatusCode statusCode, string? body = null) =>
        _responses.Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json")
        });

    /// <summary>
    ///     Creates the adapter the way the production DI does: anonymous auth (auth lives in the HTTP handler
    ///     chain), service-root base URL. Pass <paramref name="withResponseGuard" /> to include the
    ///     <see cref="ZendeskResponseGuardHandler" /> for error-translation tests.
    /// </summary>
    public IRequestAdapter CreateAdapter(bool withResponseGuard = false)
    {
        HttpMessageHandler handler = withResponseGuard
            ? new ZendeskResponseGuardHandler { InnerHandler = this }
            : this;
        return new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(),
            httpClient: new HttpClient(handler, false))
        {
            BaseUrl = BaseUrl
        };
    }

    public ZendeskSupportApiClient CreateSupportClient(bool withResponseGuard = false) =>
        new(CreateAdapter(withResponseGuard));

    public ZendeskHelpCenterApiClient CreateHelpCenterClient(bool withResponseGuard = false) =>
        new(CreateAdapter(withResponseGuard));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body,
            request.Content?.Headers.ContentType?.ToString()));

        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
    }

    internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Body, string? ContentType)
    {
        /// <summary>The absolute path, e.g. <c>/api/v2/tickets/42</c>.</summary>
        public string Path => Uri.AbsolutePath;

        /// <summary>The raw (still-encoded) query string, e.g. <c>?page%5Bsize%5D=25</c>.</summary>
        public string Query => Uri.Query;
    }
}