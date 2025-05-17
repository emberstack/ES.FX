using JetBrains.Annotations;

namespace ES.FX.Net;

/// <summary>
///     Represents configuration options for network credentials,
///     including username, password, and optional domain information.
/// </summary>
[PublicAPI]
public class NetworkCredentialOptions
{
    /// <summary>
    ///     Gets or sets the username associated with the credentials.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    ///     Gets or sets the password associated with the credentials.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     Gets or sets the domain associated with the credentials.
    ///     Used primarily for NTLM or Kerberos authentication.
    /// </summary>
    public string? Domain { get; set; }
}