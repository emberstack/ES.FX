using System.Net;
using System.Net.Http.Headers;
using ES.FX.Zendesk.Abstractions;

namespace ES.FX.Zendesk.Authentication;

/// <summary>
///     Applies the Zendesk OAuth bearer token (from the <see cref="IZendeskAccessTokenProvider" />) to every
///     outgoing request, and retries once with a freshly refreshed token if the response is <c>401 Unauthorized</c>
///     (in case the token was revoked before its cached expiry).
/// </summary>
internal sealed class ZendeskAuthenticationDelegatingHandler(IZendeskAccessTokenProvider tokenProvider)
    : DelegatingHandler
{
    /// <summary>
    ///     Request option that suppresses the bearer token for a single request. Used when fetching a
    ///     response-supplied URL (e.g. an attachment <c>content_url</c>) that resolves to a non-Zendesk host, so the
    ///     tenant's credentials are never sent to a third party.
    /// </summary>
    internal static readonly HttpRequestOptionsKey<bool> SkipAuthentication = new("ES.FX.Zendesk.SkipAuthentication");

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Options.TryGetValue(SkipAuthentication, out var skip) && skip)
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Buffer any request content up front so the one-shot 401 retry below can replay it. A forward-only
        // content stream would otherwise already be consumed by the first attempt, making the retry send a
        // corrupt/empty body (relevant once write operations ship; GETs carry no content).
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);

        var token = await tokenProvider.GetAccessTokenAsync(false, cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        // The token may have been revoked before its cached expiry — force a refresh and retry exactly once.
        response.Dispose();
        token = await tokenProvider.GetAccessTokenAsync(true, cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}