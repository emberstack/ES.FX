using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace ES.FX.NousResearch.HermesAgent.Tests.Testing;

/// <summary>
///     A stub handler for the DI registration tests. Returns a canned models list for <c>v1/models</c>, a
///     canned empty sessions list for <c>api/sessions</c> and a canned health payload for everything else,
///     while recording per-request host, absolute URI, bearer credentials and headers so keyed-instance
///     isolation can be asserted.
/// </summary>
internal sealed class RoutingHandler : HttpMessageHandler
{
    public ConcurrentBag<RecordedRequest> Requests { get; } = [];

    public string? LastAuthorizationScheme { get; private set; }
    public string? LastAuthorizationParameter { get; private set; }
    public string? LastAccept { get; private set; }
    public string? LastUserAgent { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;

        LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
        LastAuthorizationParameter = request.Headers.Authorization?.Parameter;
        LastAccept = request.Headers.Accept.ToString();
        LastUserAgent = request.Headers.UserAgent.ToString();
        Requests.Add(new RecordedRequest(uri.Host, uri.AbsoluteUri, request.Headers.Authorization?.Parameter));

        if (uri.AbsolutePath.EndsWith("api/sessions", StringComparison.Ordinal))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    @object = "list",
                    data = Array.Empty<object>(),
                    limit = 50,
                    offset = 0,
                    has_more = false
                })
            });

        if (uri.AbsolutePath.EndsWith("v1/models", StringComparison.Ordinal))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    @object = "list",
                    data = new[]
                    {
                        new { id = "hermes-agent", @object = "model", owned_by = "hermes", root = "hermes-agent" }
                    }
                })
            });

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { status = "ok", platform = "hermes-agent", version = "dev" })
        });
    }

    internal sealed record RecordedRequest(string Host, string AbsoluteUri, string? BearerKey);
}