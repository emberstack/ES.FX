using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ES.FX.Zendesk.Tests.Testing;

/// <summary>
///     A stub handler that routes by path: the OAuth token endpoint mints a token correlated with the
///     <c>client_id</c> presented in the request body (<c>tok-{client_id}</c>), everything else returns a
///     configurable API response (a canned ticket envelope by default). Records the token-call count, the last
///     API authorization scheme, the API hosts seen and every API request (host + path + bearer token) so
///     per-instance credential isolation and handler-chain behavior can be asserted.
/// </summary>
internal sealed class RoutingHandler : HttpMessageHandler
{
    private int _tokenCalls;

    public int TokenCalls => Volatile.Read(ref _tokenCalls);
    public string? LastApiAuthScheme { get; private set; }
    public string? LastApiAccept { get; private set; }
    public string? LastApiUserAgent { get; private set; }
    public string? LastTokenUserAgent { get; private set; }

    /// <summary>The status the API branch replies with — set to a non-success value for guard-handler tests.</summary>
    public HttpStatusCode ApiStatusCode { get; set; } = HttpStatusCode.OK;

    /// <summary>The JSON the API branch replies with.</summary>
    public string ApiResponseJson { get; set; } = """{ "ticket": { "id": 1, "subject": "Canned" } }""";

    public ConcurrentBag<string> ApiHosts { get; } = [];
    public ConcurrentBag<RecordedApiRequest> ApiRequests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;

        if (uri.AbsolutePath.Contains("oauth/tokens", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _tokenCalls);
            LastTokenUserAgent = request.Headers.UserAgent.ToString();

            // Mint a token derived from the client_id that was actually presented, so tests can prove an API
            // request carries the bearer acquired with ITS OWN credentials (cross-tenant leak guard).
            var clientId = await ReadClientIdAsync(request, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new
                {
                    access_token = clientId is null ? "tok-1" : $"tok-{clientId}",
                    token_type = "bearer",
                    scope = "read",
                    expires_in = 1800
                })
            };
        }

        LastApiAuthScheme = request.Headers.Authorization?.Scheme;
        LastApiAccept = request.Headers.Accept.ToString();
        LastApiUserAgent = request.Headers.UserAgent.ToString();
        ApiHosts.Add(uri.Host);
        ApiRequests.Add(new RecordedApiRequest(uri.Host, uri.AbsolutePath, uri.AbsoluteUri,
            request.Headers.Authorization?.Parameter));
        return new HttpResponseMessage(ApiStatusCode)
        {
            Content = new StringContent(ApiResponseJson, Encoding.UTF8, "application/json")
        };
    }

    private static async Task<string?> ReadClientIdAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content is null) return null;
        var body = await request.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.TryGetProperty("client_id", out var clientId) ? clientId.GetString() : null;
    }

    internal sealed record RecordedApiRequest(string Host, string Path, string AbsoluteUri, string? BearerToken);
}