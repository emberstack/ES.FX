namespace ES.FX.Zendesk.Configuration;

/// <summary>
///     OAuth 2.0 <c>client_credentials</c> options for a Zendesk confidential client. Legacy API-token
///     authentication has been removed (Zendesk is sunsetting API tokens); OAuth is the only supported mechanism.
/// </summary>
public class ZendeskOAuthOptions
{
    /// <summary>
    ///     The OAuth client's unique identifier (<c>client_id</c>) from the Zendesk confidential OAuth client.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    ///     The OAuth client's secret (<c>client_secret</c>).
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    ///     The space-separated OAuth scopes to request. Defaults to <c>read</c> — see
    ///     <c>ZendeskOAuthScopes</c>. Use e.g. <c>read write</c> or resource scopes (<c>users:read</c>,
    ///     <c>tickets:write</c>, ...) when write operations are used.
    /// </summary>
    public string? Scope { get; set; } = ZendeskOAuthScopes.Read;

    /// <summary>
    ///     Optional requested token lifetime in seconds (Zendesk accepts 300–172800). When <c>null</c>, Zendesk's
    ///     default applies (30 minutes for recent clients).
    /// </summary>
    public int? ExpiresIn { get; set; }

    /// <summary>
    ///     How long before the real expiry a cached token is treated as stale, so it is refreshed proactively
    ///     rather than expiring mid-request. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan ExpiryBuffer { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Optional explicit token endpoint override (e.g. for a sandbox or a test double). When <c>null</c>, it is
    ///     derived as <c>/oauth/tokens</c> on the resolved base address host (the <c>BaseUrl</c> override when set,
    ///     otherwise <c>https://{subdomain}.zendesk.com</c>).
    /// </summary>
    public Uri? TokenEndpoint { get; set; }
}