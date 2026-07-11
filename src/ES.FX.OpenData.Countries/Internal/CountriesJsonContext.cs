using System.Text.Json.Serialization;

namespace ES.FX.OpenData.Countries.Internal;

// The embedded localized-names overlay maps ISO 3166-1 alpha-2 code -> (culture name -> localized name).
// English names are NOT stored here — they come from the ISO 3166-1 dataset (single source of identity).
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>),
    TypeInfoPropertyName = "LocalizedNamesOverlay")]
internal partial class CountriesJsonContext : JsonSerializerContext;