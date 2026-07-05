using System.Net.Http.Headers;
using ES.FX.NousResearch.HermesAgent.Configuration;
using Microsoft.Extensions.Options;

namespace ES.FX.NousResearch.HermesAgent.Authentication;

/// <summary>
///     Applies the static Hermes Agent API key (<c>Authorization: Bearer {ApiKey}</c>) to every outgoing
///     request. The key is read from the named <see cref="HermesAgentClientOptions" /> per request (not baked
///     into <c>DefaultRequestHeaders</c>), so a configuration reload takes effect without recycling the handler
///     chain.
/// </summary>
internal sealed class HermesAgentAuthenticationDelegatingHandler(
    IOptionsMonitor<HermesAgentClientOptions> options,
    string optionsName) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var apiKey = options.Get(optionsName).ApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return base.SendAsync(request, cancellationToken);
    }
}