using System.Text.Json.Serialization;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf.Internal;

internal sealed class AnafTvaRequestItem
{
    [JsonPropertyName("cui")] public long Cui { get; set; }
    [JsonPropertyName("data")] public string Data { get; set; } = "";
}

internal sealed class AnafTvaResponse
{
    [JsonPropertyName("cod")] public int Cod { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("found")] public AnafFoundItem[]? Found { get; set; }
    [JsonPropertyName("notFound")] public AnafNotFoundItem[]? NotFound { get; set; }
}

internal sealed class AnafFoundItem
{
    [JsonPropertyName("date_generale")] public AnafDateGenerale? DateGenerale { get; set; }
    [JsonPropertyName("inregistrare_scop_Tva")] public AnafScopTva? InregistrareScopTva { get; set; }
    [JsonPropertyName("stare_inactiv")] public AnafStareInactiv? StareInactiv { get; set; }
}

internal sealed class AnafDateGenerale
{
    [JsonPropertyName("cui")] public long Cui { get; set; }
    [JsonPropertyName("denumire")] public string? Denumire { get; set; }
    [JsonPropertyName("adresa")] public string? Adresa { get; set; }
    [JsonPropertyName("nrRegCom")] public string? NrRegCom { get; set; }
    [JsonPropertyName("telefon")] public string? Telefon { get; set; }
}

internal sealed class AnafScopTva
{
    [JsonPropertyName("scpTVA")] public bool ScpTva { get; set; }
}

internal sealed class AnafStareInactiv
{
    [JsonPropertyName("statusInactivi")] public bool StatusInactivi { get; set; }
}

internal sealed class AnafNotFoundItem
{
    [JsonPropertyName("cui")] public long Cui { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AnafTvaRequestItem[]))]
[JsonSerializable(typeof(AnafTvaResponse))]
internal partial class AnafJsonContext : JsonSerializerContext;
