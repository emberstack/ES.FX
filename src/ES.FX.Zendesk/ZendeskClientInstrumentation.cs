using System.Diagnostics;

namespace ES.FX.Zendesk;

/// <summary>
///     Diagnostics for the Zendesk API client. Exposes the <see cref="ActivitySource" /> name so it can be wired
///     into OpenTelemetry tracing (see the <c>ES.FX.Ignite.Zendesk</c> Spark).
/// </summary>
public static class ZendeskClientInstrumentation
{
    /// <summary>
    ///     The name of the <see cref="System.Diagnostics.ActivitySource" /> the client emits spans on.
    /// </summary>
    public const string ActivitySourceName = "ES.FX.Zendesk";

    /// <summary>
    ///     The shared <see cref="System.Diagnostics.ActivitySource" /> used by the client.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}