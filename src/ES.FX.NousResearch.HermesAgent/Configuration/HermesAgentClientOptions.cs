namespace ES.FX.NousResearch.HermesAgent.Configuration;

/// <summary>
///     Configuration for connecting to a Hermes Agent API server.
/// </summary>
public class HermesAgentClientOptions
{
    /// <summary>
    ///     The absolute http(s) base URL of the Hermes Agent API server (e.g. <c>http://localhost:8642</c>).
    ///     Required.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    ///     The static API server key sent as <c>Authorization: Bearer {ApiKey}</c> on every request. Required —
    ///     the server refuses to start without a configured key, so in practice authentication is always on.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    ///     Resolves the API base address (always ending with a trailing slash, so relative request URIs compose
    ///     correctly).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <see cref="BaseUrl" /> is <c>null</c> or whitespace. In the DI flow the options validator
    ///     guarantees it is set before the client is built.
    /// </exception>
    public Uri GetBaseAddress()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("A Hermes Agent BaseUrl must be configured.");

        var baseUrl = BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/";
        return new Uri(baseUrl, UriKind.Absolute);
    }
}