using System.Diagnostics;

namespace ES.FX.NousResearch.HermesAgent;

/// <summary>
///     Diagnostics for the Hermes Agent API client. Exposes the <see cref="ActivitySource" /> name so it can be
///     wired into OpenTelemetry tracing (see the <c>ES.FX.Ignite.NousResearch.HermesAgent</c> Spark).
/// </summary>
public static class HermesAgentClientInstrumentation
{
    /// <summary>
    ///     The name of the <see cref="System.Diagnostics.ActivitySource" /> the client emits spans on.
    /// </summary>
    public const string ActivitySourceName = "ES.FX.NousResearch.HermesAgent";

    /// <summary>
    ///     The shared <see cref="System.Diagnostics.ActivitySource" /> used by the client.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
