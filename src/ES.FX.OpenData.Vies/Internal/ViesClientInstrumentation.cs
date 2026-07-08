using System.Diagnostics;

namespace ES.FX.OpenData.Vies.Internal;

/// <summary>
///     The <see cref="ActivitySource" /> for the VIES client. Subscribe to the source named
///     <c>"ES.FX.OpenData.Vies"</c> (e.g. <c>tracing.AddSource("ES.FX.OpenData.Vies")</c>) to capture spans.
/// </summary>
internal static class ViesClientInstrumentation
{
    public const string ActivitySourceName = "ES.FX.OpenData.Vies";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
