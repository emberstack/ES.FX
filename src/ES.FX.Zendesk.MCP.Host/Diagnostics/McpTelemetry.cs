namespace ES.FX.Zendesk.MCP.Host.Diagnostics;

/// <summary>
///     Telemetry identifiers emitted by the <c>ModelContextProtocol</c> SDK, wired into OpenTelemetry.
/// </summary>
/// <remarks>
///     The <c>Experimental.</c> prefix is version-sensitive and is expected to change in a future SDK release.
/// </remarks>
public static class McpTelemetry
{
    /// <summary>The name of the <see cref="System.Diagnostics.ActivitySource" /> the SDK emits traces on.</summary>
    public const string ActivitySourceName = "Experimental.ModelContextProtocol";

    /// <summary>The name of the <see cref="System.Diagnostics.Metrics.Meter" /> the SDK emits metrics on.</summary>
    public const string MeterName = "Experimental.ModelContextProtocol";
}