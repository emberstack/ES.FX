using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace ES.FX.Extensions.System.Text.Json.Serialization;

[PublicAPI]
public static class JsonSerializerExtendedOptions
{
    public static JsonSerializerOptions WebApi { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonSerializerOptions.Web.PropertyNamingPolicy, false) }
    };
}