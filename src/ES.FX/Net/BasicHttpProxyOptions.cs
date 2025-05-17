using JetBrains.Annotations;

namespace ES.FX.Net;

/// <summary>
///     Represents configuration options for an HTTP proxy,
///     including address, bypass rules, credential usage, and authentication details.
/// </summary>
[PublicAPI]
public class BasicHttpProxyOptions
{
    /// <summary>
    ///     Gets or sets the URI address of the proxy server.
    ///     Example: <c>http://proxy.example.com:8080</c>.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to bypass the proxy server for local addresses.
    /// </summary>
    public bool BypassProxyOnLocal { get; set; }

    /// <summary>
    ///     Gets or sets the list of addresses that should bypass the proxy server.
    ///     Wildcard patterns can be used.
    /// </summary>
    public string[]? BypassList { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the default system credentials should be used for authentication with the
    ///     proxy server.
    /// </summary>
    public bool UseDefaultCredentials { get; set; }

    /// <summary>
    ///     Gets or sets the network credentials to use for authenticating with the proxy server.
    ///     If <see cref="UseDefaultCredentials" /> is <c>false</c> and credentials are provided, these will be used.
    /// </summary>
    public NetworkCredentialOptions? Credentials { get; set; }
}