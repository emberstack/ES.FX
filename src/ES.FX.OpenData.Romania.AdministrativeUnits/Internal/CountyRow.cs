using System.Text.Json.Serialization;

namespace ES.FX.OpenData.Romania.AdministrativeUnits.Internal;

internal sealed class CountyRow
{
    public int SirutaCode { get; set; }
    public string Abbreviation { get; set; } = "";
    public string IsoCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string ResidenceName { get; set; } = "";
    public string[] NationalIdSeries { get; set; } = [];
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CountyRow[]))]
internal partial class RomanianAdministrativeUnitsJsonContext : JsonSerializerContext;
