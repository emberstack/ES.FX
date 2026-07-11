using System.Text.Json.Serialization;

namespace ES.FX.OpenData.Countries.ISO3166.Internal;

// DTOs mirror the embedded JSON verbatim (snake_case keys, numeric codes as strings with leading zeros). They
// are mapped to the public models by the stores. Each file's root is a single-property object whose key is the
// standard's number (e.g. "3166-1"), modelled by the *Document wrappers below.

internal sealed class Iso3166CountryRow
{
    [JsonPropertyName("alpha_2")] public string Alpha2 { get; set; } = "";
    [JsonPropertyName("alpha_3")] public string Alpha3 { get; set; } = "";
    [JsonPropertyName("numeric")] public string Numeric { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("official_name")] public string? OfficialName { get; set; }
    [JsonPropertyName("common_name")] public string? CommonName { get; set; }
    [JsonPropertyName("flag")] public string? Flag { get; set; }
}

internal sealed class Iso3166CountrySubdivisionRow
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("parent")] public string? Parent { get; set; }
}

internal sealed class Iso3166FormerCountryRow
{
    [JsonPropertyName("alpha_4")] public string Alpha4 { get; set; } = "";
    [JsonPropertyName("alpha_3")] public string Alpha3 { get; set; } = "";
    [JsonPropertyName("alpha_2")] public string? Alpha2 { get; set; }
    [JsonPropertyName("numeric")] public string? Numeric { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("comment")] public string? Comment { get; set; }
    [JsonPropertyName("withdrawal_date")] public string? WithdrawalDate { get; set; }
}

internal sealed class Iso3166Part1Document
{
    [JsonPropertyName("3166-1")] public Iso3166CountryRow[] Entries { get; set; } = [];
}

internal sealed class Iso3166Part2Document
{
    [JsonPropertyName("3166-2")] public Iso3166CountrySubdivisionRow[] Entries { get; set; } = [];
}

internal sealed class Iso3166Part3Document
{
    [JsonPropertyName("3166-3")] public Iso3166FormerCountryRow[] Entries { get; set; } = [];
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Iso3166Part1Document))]
[JsonSerializable(typeof(Iso3166Part2Document))]
[JsonSerializable(typeof(Iso3166Part3Document))]
internal partial class Iso3166JsonContext : JsonSerializerContext;