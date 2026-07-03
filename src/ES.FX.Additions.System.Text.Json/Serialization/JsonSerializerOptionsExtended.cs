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
    ///     <see cref="JsonSerializerOptions.Default" />. The converter does not allow integer values for enums;
    ///     enum values must be supplied as strings (matched case-insensitively).
    ///     <para>
    ///         This instance is read-only (see <see cref="JsonSerializerOptions.MakeReadOnly()" />) to prevent
    ///         process-wide mutation and first-use races. To customize, copy-construct a new instance, for example
    ///         <c>new JsonSerializerOptions(WebApi)</c>, and mutate the copy.
    ///     </para>
    /// </remarks>
    public static JsonSerializerOptions WebApi { get; } = CreateReadOnly(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonSerializerOptions.Default.PropertyNamingPolicy, false) }
        });


    /// <summary>
    ///     Gets the <see cref="JsonSerializerOptions" /> preconfigured for JavaScript-oriented Web API usage.
    /// </summary>
    /// <remarks>
    ///     The options are initialized with the <see cref="JsonSerializerDefaults.Web" /> settings and include a
    ///     <see cref="JsonStringEnumConverter" /> that uses the property naming policy defined by
    ///     <see cref="JsonSerializerOptions.Web" />. The converter does not allow integer values for enums;
    ///     enum values must be supplied as strings (matched case-insensitively).
    ///     <para>
    ///         This instance is read-only (see <see cref="JsonSerializerOptions.MakeReadOnly()" />) to prevent
    ///         process-wide mutation and first-use races. To customize, copy-construct a new instance, for example
    ///         <c>new JsonSerializerOptions(JavascriptWebApi)</c>, and mutate the copy.
    ///     </para>
    /// </remarks>
    public static JsonSerializerOptions JavascriptWebApi { get; } = CreateReadOnly(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter(JsonSerializerOptions.Web.PropertyNamingPolicy, false) }
        });


    /// <summary>
    ///     Gets the <see cref="JsonSerializerOptions" /> preconfigured for payload serialization
    /// </summary>
    /// <remarks>
    ///     The options are initialized with the <see cref="JsonSerializerDefaults.General" /> settings, enable
    ///     case-insensitive property name matching, and include a <see cref="JsonStringEnumConverter" /> that uses
    ///     the property naming policy defined by <see cref="JsonSerializerOptions.Default" />. The converter allows
    ///     both string and integer values for enums; string values are matched case-insensitively.
    ///     <para>
    ///         This instance is read-only (see <see cref="JsonSerializerOptions.MakeReadOnly()" />) to prevent
    ///         process-wide mutation and first-use races. To customize, copy-construct a new instance, for example
    ///         <c>new JsonSerializerOptions(Payload)</c>, and mutate the copy.
    ///     </para>
    /// </remarks>
    public static JsonSerializerOptions Payload { get; } = CreateReadOnly(
        new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonSerializerOptions.Default.PropertyNamingPolicy) }
        });

    /// <summary>
    ///     Seals the supplied <paramref name="options" /> and returns the same instance. Freezes via
    ///     <c>MakeReadOnly(populateMissingResolver: true)</c> so the default reflection-based type-info resolver is
    ///     attached first — the parameterless <c>MakeReadOnly()</c> throws when no <c>TypeInfoResolver</c> has been set.
    /// </summary>
    private static JsonSerializerOptions CreateReadOnly(JsonSerializerOptions options)
    {
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}