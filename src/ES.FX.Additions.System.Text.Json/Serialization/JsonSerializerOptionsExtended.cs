using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace ES.FX.Additions.System.Text.Json.Serialization;

/// <summary>
///     Provides extended JSON serializer options configured for standard usage.
/// </summary>
/// <remarks>
///     This class exposes preconfigured <see cref="JsonSerializerOptions" /> based on the
///     <see cref="JsonSerializerDefaults.Web" /> defaults, with additional converters tailored for specific scenarios.
/// </remarks>
[PublicAPI]
public static class JsonSerializerOptionsExtended
{
    /// <summary>
    ///     Gets the <see cref="JsonSerializerOptions" /> preconfigured for Web API usage.
    /// </summary>
    /// <remarks>
    ///     The options are initialized with the <see cref="JsonSerializerDefaults.Web" /> settings and include a
    ///     <see cref="JsonStringEnumConverter" /> that uses the property naming policy defined by
    ///     <see cref="JsonSerializerOptions.Web" />. The converter is configured for case-sensitive enum conversion.
    /// </remarks>
    public static JsonSerializerOptions WebApi { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonSerializerOptions.Web.PropertyNamingPolicy, false) }
    };
}