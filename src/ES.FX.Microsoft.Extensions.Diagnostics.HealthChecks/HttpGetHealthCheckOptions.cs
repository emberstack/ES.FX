namespace ES.FX.Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
///     Options for the <see cref="HttpGetHealthCheck" />.
/// </summary>
public class HttpGetHealthCheckOptions
{
    /// <summary>
    ///     The URI to check.
    /// </summary>
    public required string Uri { get; set; }
}