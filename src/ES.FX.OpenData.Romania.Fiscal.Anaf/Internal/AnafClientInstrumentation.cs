using System.Diagnostics;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf.Internal;

/// <summary>
///     The <see cref="ActivitySource" /> for the ANAF client. Subscribe to the source named
///     <c>"ES.FX.OpenData.Romania.Fiscal.Anaf"</c> to capture spans.
/// </summary>
internal static class AnafClientInstrumentation
{
    public const string ActivitySourceName = "ES.FX.OpenData.Romania.Fiscal.Anaf";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
