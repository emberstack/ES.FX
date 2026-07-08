using System.Text.Json.Serialization;

namespace ES.FX.OpenData.Countries.Internal;

internal sealed class CountryRow
{
    public int NumericCode { get; set; }
    public string Alpha2 { get; set; } = "";
    public string Alpha3 { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameRo { get; set; } = "";
}

internal sealed class CountryAliasRow
{
    public string Code { get; set; } = "";
    public string Alpha2 { get; set; } = "";
    public string Alpha3 { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameRo { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CountryRow[]))]
[JsonSerializable(typeof(CountryAliasRow[]))]
internal partial class CountriesJsonContext : JsonSerializerContext;
