namespace ES.FX.Zendesk.Configuration;

/// <summary>
///     Configuration for connecting to a Zendesk account.
/// </summary>
public class ZendeskClientOptions
{
    /// <summary>
    ///     The Zendesk account subdomain (the <c>{subdomain}</c> in <c>https://{subdomain}.zendesk.com</c>).
    /// </summary>
    public string Subdomain { get; set; } = string.Empty;

    /// <summary>
    ///     An optional explicit base URL override (e.g. for a sandbox or a test double). When set, it takes
    ///     precedence over <see cref="Subdomain" />. The resolved base address always targets the <c>/api/v2/</c> path.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    ///     OAuth 2.0 <c>client_credentials</c> options.
    /// </summary>
    public ZendeskOAuthOptions OAuth { get; set; } = new();

    /// <summary>
    ///     Resolves the API base address (always ending with a trailing slash).
    /// </summary>
    public Uri GetBaseAddress()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            var baseUrl = BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/";
            return new Uri(baseUrl, UriKind.Absolute);
        }

        return new Uri($"https://{Subdomain}.zendesk.com/api/v2/", UriKind.Absolute);
    }

    /// <summary>
    ///     Resolves the service root (the base address without the trailing <c>api/v2/</c> segment). The generated
    ///     Kiota clients carry the full <c>/api/v2/…</c> path in their request templates, so their
    ///     <c>RequestAdapter.BaseUrl</c> must target the host root — while preserving any extra path prefix a
    ///     <see cref="BaseUrl" /> override (proxy, test double) may carry.
    /// </summary>
    public Uri GetServiceRootAddress()
    {
        var baseAddress = GetBaseAddress().ToString();
        // Match "/api/v2/" at a segment boundary (never inside a segment like "myapi/v2/"), then strip the
        // trailing "api/v2/" while keeping the boundary slash.
        const string apiSuffix = "/api/v2/";
        if (baseAddress.EndsWith(apiSuffix, StringComparison.OrdinalIgnoreCase))
            baseAddress = baseAddress[..^(apiSuffix.Length - 1)];
        return new Uri(baseAddress, UriKind.Absolute);
    }

    /// <summary>
    ///     Resolves the OAuth token endpoint: <see cref="ZendeskOAuthOptions.TokenEndpoint" /> when set, otherwise
    ///     <c>/oauth/tokens</c> on the same host as <see cref="GetBaseAddress" /> (so a <see cref="BaseUrl" />
    ///     override — sandbox or test double — also redirects token requests, and credentials are never sent to a
    ///     host other than the configured one).
    /// </summary>
    public Uri GetOAuthTokenEndpoint() =>
        OAuth.TokenEndpoint ?? new Uri(GetBaseAddress(), "/oauth/tokens");
}