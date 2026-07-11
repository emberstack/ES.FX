using System.Diagnostics;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck.Internal;

/// <summary>
///     The <see cref="ActivitySource" /> for the ANAF client. Subscribe to the source named
///     <c>"ES.FX.OpenData.Romania.Anaf.VatCheck"</c> to capture spans.
/// </summary>
internal static class AnafVatCheckClientInstrumentation
{
    public const string ActivitySourceName = "ES.FX.OpenData.Romania.Anaf.VatCheck";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}