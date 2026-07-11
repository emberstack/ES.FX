using System.Text.Json.Serialization;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck.Internal;

internal sealed class AnafTvaRequestItem
{
    [JsonPropertyName("cui")] public long Cui { get; set; }
    [JsonPropertyName("data")] public string Data { get; set; } = "";
}

internal sealed class AnafTvaResponse
{
    // v9 omits "cod"/"message" on success (found/notFound only); a code is present only on error/legacy responses.
    [JsonPropertyName("cod")] public int? Cod { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("found")] public AnafFoundItem[]? Found { get; set; }
    [JsonPropertyName("notFound")] public AnafNotFoundItem[]? NotFound { get; set; }
}

internal sealed class AnafFoundItem
{
    [JsonPropertyName("date_generale")] public AnafDateGenerale? DateGenerale { get; set; }

    [JsonPropertyName("inregistrare_scop_Tva")]
    public AnafScopTva? InregistrareScopTva { get; set; }

    [JsonPropertyName("inregistrare_RTVAI")]
    public AnafRtvai? InregistrareRtvai { get; set; }

    [JsonPropertyName("stare_inactiv")] public AnafStareInactiv? StareInactiv { get; set; }

    [JsonPropertyName("inregistrare_SplitTVA")]
    public AnafSplitTva? InregistrareSplitTva { get; set; }

    [JsonPropertyName("adresa_sediu_social")]
    public AnafAdresaSediu? AdresaSediuSocial { get; set; }

    [JsonPropertyName("adresa_domiciliu_fiscal")]
    public AnafAdresaDomiciliu? AdresaDomiciliuFiscal { get; set; }
}

internal sealed class AnafDateGenerale
{
    [JsonPropertyName("cui")] public long Cui { get; set; }
    [JsonPropertyName("denumire")] public string? Denumire { get; set; }
    [JsonPropertyName("adresa")] public string? Adresa { get; set; }
    [JsonPropertyName("telefon")] public string? Telefon { get; set; }
    [JsonPropertyName("fax")] public string? Fax { get; set; }
    [JsonPropertyName("codPostal")] public string? CodPostal { get; set; }
    [JsonPropertyName("act")] public string? Act { get; set; }

    [JsonPropertyName("stare_inregistrare")]
    public string? StareInregistrare { get; set; }

    [JsonPropertyName("organFiscalCompetent")]
    public string? OrganFiscalCompetent { get; set; }

    [JsonPropertyName("forma_de_proprietate")]
    public string? FormaDeProprietate { get; set; }

    [JsonPropertyName("forma_organizare")] public string? FormaOrganizare { get; set; }
    [JsonPropertyName("forma_juridica")] public string? FormaJuridica { get; set; }
    [JsonPropertyName("nrRegCom")] public string? NrRegCom { get; set; }
    [JsonPropertyName("cod_CAEN")] public string? CodCaen { get; set; }
    [JsonPropertyName("iban")] public string? Iban { get; set; }

    [JsonPropertyName("data_inregistrare")]
    public string? DataInregistrare { get; set; }

    [JsonPropertyName("statusRO_e_Factura")]
    public bool StatusRoEFactura { get; set; }
}

internal sealed class AnafScopTva
{
    [JsonPropertyName("scpTVA")] public bool ScpTva { get; set; }
    [JsonPropertyName("perioade_TVA")] public AnafPerioadaTva[]? PerioadeTva { get; set; }
}

internal sealed class AnafPerioadaTva
{
    [JsonPropertyName("data_inceput_ScpTVA")]
    public string? DataInceput { get; set; }

    [JsonPropertyName("data_sfarsit_ScpTVA")]
    public string? DataSfarsit { get; set; }

    [JsonPropertyName("mesaj_ScpTVA")] public string? Mesaj { get; set; }
}

internal sealed class AnafRtvai
{
    [JsonPropertyName("statusTvaIncasare")]
    public bool StatusTvaIncasare { get; set; }
}

internal sealed class AnafStareInactiv
{
    [JsonPropertyName("statusInactivi")] public bool StatusInactivi { get; set; }
}

internal sealed class AnafSplitTva
{
    [JsonPropertyName("statusSplitTVA")] public bool StatusSplitTva { get; set; }
}

// The two address blocks carry the same fields under different key prefixes (s* = sediu social, d* = domiciliu
// fiscal); a shared interface lets the client map both with one method.
internal interface IAnafVatCheckAddress
{
    string? Strada { get; }
    string? NumarStrada { get; }
    string? Localitate { get; }
    string? CodLocalitate { get; }
    string? Judet { get; }
    string? CodJudet { get; }
    string? CodJudetAuto { get; }
    string? Detalii { get; }
    string? CodPostal { get; }
    string? Tara { get; }
}

internal sealed class AnafAdresaSediu : IAnafVatCheckAddress
{
    [JsonPropertyName("sdenumire_Strada")] public string? Strada { get; set; }
    [JsonPropertyName("snumar_Strada")] public string? NumarStrada { get; set; }

    [JsonPropertyName("sdenumire_Localitate")]
    public string? Localitate { get; set; }

    [JsonPropertyName("scod_Localitate")] public string? CodLocalitate { get; set; }
    [JsonPropertyName("sdenumire_Judet")] public string? Judet { get; set; }
    [JsonPropertyName("scod_Judet")] public string? CodJudet { get; set; }
    [JsonPropertyName("scod_JudetAuto")] public string? CodJudetAuto { get; set; }
    [JsonPropertyName("sdetalii_Adresa")] public string? Detalii { get; set; }
    [JsonPropertyName("scod_Postal")] public string? CodPostal { get; set; }
    [JsonPropertyName("stara")] public string? Tara { get; set; }
}

internal sealed class AnafAdresaDomiciliu : IAnafVatCheckAddress
{
    [JsonPropertyName("ddenumire_Strada")] public string? Strada { get; set; }
    [JsonPropertyName("dnumar_Strada")] public string? NumarStrada { get; set; }

    [JsonPropertyName("ddenumire_Localitate")]
    public string? Localitate { get; set; }

    [JsonPropertyName("dcod_Localitate")] public string? CodLocalitate { get; set; }
    [JsonPropertyName("ddenumire_Judet")] public string? Judet { get; set; }
    [JsonPropertyName("dcod_Judet")] public string? CodJudet { get; set; }
    [JsonPropertyName("dcod_JudetAuto")] public string? CodJudetAuto { get; set; }
    [JsonPropertyName("ddetalii_Adresa")] public string? Detalii { get; set; }
    [JsonPropertyName("dcod_Postal")] public string? CodPostal { get; set; }
    [JsonPropertyName("dtara")] public string? Tara { get; set; }
}

internal sealed class AnafNotFoundItem
{
    [JsonPropertyName("cui")] public long Cui { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AnafTvaRequestItem[]))]
[JsonSerializable(typeof(AnafTvaResponse))]
internal partial class AnafVatCheckJsonContext : JsonSerializerContext;