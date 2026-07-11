using System.Text.Json.Serialization;

namespace ES.FX.OpenData.Currencies.Internal;

// The embedded localized-names overlay maps ISO 4217 alpha-3 code -> (culture name -> localized name).
// English names are NOT stored here — they come from the ISO 4217 dataset (single source of identity).
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>),
    TypeInfoPropertyName = "LocalizedNamesOverlay")]
internal partial class CurrenciesJsonContext : JsonSerializerContext;