using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace ES.FX.Zendesk.Tests.Testing;

/// <summary>
///     A stub handler that routes by path: the OAuth token endpoint returns a token, everything else returns a
///     canned user. Records the token-call count, the last API authorization scheme, and the API hosts seen.
/// </summary>
internal sealed class RoutingHandler : HttpMessageHandler
{
    private int _tokenCalls;

    public int TokenCalls => Volatile.Read(ref _tokenCalls);
    public string? LastApiAuthScheme { get; private set; }
    public string? LastApiAccept { get; private set; }
    public string? LastApiUserAgent { get; private set; }
    public string? LastTokenUserAgent { get; private set; }
    public ConcurrentBag<string> ApiHosts { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;

        if (uri.AbsolutePath.Contains("oauth/tokens", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _tokenCalls);
            LastTokenUserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new
                {
                    access_token = "tok-1", token_type = "bearer", scope = "read", expires_in = 1800
                })
            });
        }

        LastApiAuthScheme = request.Headers.Authorization?.Scheme;
        LastApiAccept = request.Headers.Accept.ToString();
        LastApiUserAgent = request.Headers.UserAgent.ToString();
        ApiHosts.Add(uri.Host);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                user = new { id = 1, name = "Agent", email = "agent@acme.com", role = "admin" }
            })
        });
    }
}