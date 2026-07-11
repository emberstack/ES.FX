using System.Text.Json.Serialization;

namespace ES.FX.OpenData.Currencies.ISO4217.Internal;

// DTO mirrors the embedded JSON verbatim (snake_case keys, numeric code as a zero-padded string). It is mapped
// to the public model by the store. The file's root is a single-property object keyed by the standard number.

internal sealed class Iso4217CurrencyRow
{
    [JsonPropertyName("alpha_3")] public string Alpha3 { get; set; } = "";
    [JsonPropertyName("numeric")] public string Numeric { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

internal sealed class Iso4217Document
{
    [JsonPropertyName("4217")] public Iso4217CurrencyRow[] Entries { get; set; } = [];
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Iso4217Document))]
internal partial class Iso4217JsonContext : JsonSerializerContext;