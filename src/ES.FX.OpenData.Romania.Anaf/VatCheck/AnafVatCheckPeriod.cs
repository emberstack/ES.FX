using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck;

/// <summary>A VAT-registration period from ANAF (<c>perioade_TVA</c>). Dates are the raw ANAF strings for now.</summary>
[PublicAPI]
public sealed record AnafVatCheckPeriod
{
    /// <summary>The VAT-registration start date (<c>data_inceput_ScpTVA</c>).</summary>
    public string? StartDate { get; init; }

    /// <summary>The VAT-registration end date (<c>data_sfarsit_ScpTVA</c>), empty while still active.</summary>
    public string? EndDate { get; init; }

    /// <summary>An optional message about the period (<c>mesaj_ScpTVA</c>).</summary>
    public string? Message { get; init; }
}