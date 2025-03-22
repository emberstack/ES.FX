using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace ES.FX.Text.Json.Serialization;

[PublicAPI]
public static class JsonSerializerExtensions
{
    /// <summary>
    ///     <inheritdoc cref="JsonSerializer.Deserialize{TValue}(string,System.Text.Json.JsonSerializerOptions?)" />
    /// </summary>
    /// <remarks>
    ///     This uses <see cref="JsonSerializerOptions.Web" /> by if no value is supplied for
    ///     <param name="options" />
    /// </remarks>
    public static bool TryDeserialize<T>([StringSyntax(StringSyntaxAttribute.Json)] this string json, out T? result,
        JsonSerializerOptions? options = null)
    {
        try
        {
            result = JsonSerializer.Deserialize<T>(json, options ?? JsonSerializerOptions.Web);
            return result is not null;
        }
        catch (Exception)
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    ///     <inheritdoc cref="JsonSerializer.Deserialize{TValue}(Stream,System.Text.Json.JsonSerializerOptions?)" />
    /// </summary>
    /// <remarks>
    ///     This uses <see cref="JsonSerializerOptions.Web" /> by if no value is supplied for
    ///     <param name="options" />
    /// </remarks>
    public static bool TryDeserialize<T>(this Stream utf8Json, [NotNullWhen(true)] out T? result,
        JsonSerializerOptions? options = null)
    {
        try
        {
            result = JsonSerializer.Deserialize<T>(utf8Json, options ?? JsonSerializerOptions.Web);
            return result is not null;
        }
        catch (Exception)
        {
            result = default;
            return false;
        }
    }
}