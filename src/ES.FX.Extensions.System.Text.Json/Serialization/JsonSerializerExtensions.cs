using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace ES.FX.Extensions.System.Text.Json.Serialization;

/// <summary>
///     Provides extension methods for JSON deserialization using <see cref="JsonSerializer" />.
/// </summary>
[PublicAPI]
public static class JsonSerializerExtensions
{
    /// <summary>
    ///     Attempts to deserialize the specified JSON string into an instance of type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type into which to deserialize the JSON.</typeparam>
    /// <param name="json">A string containing JSON data.</param>
    /// <param name="result">
    ///     When this method returns, contains the deserialized object of type <typeparamref name="T" />,
    ///     if the deserialization succeeded, or the default value of <typeparamref name="T" /> if it failed.
    /// </param>
    /// <param name="options">
    ///     The <see cref="JsonSerializerOptions" /> to use during deserialization. If <c>null</c>,
    ///     <see cref="JsonSerializerOptions.Web" /> is used.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the JSON string was successfully deserialized into an instance of <typeparamref name="T" />;
    ///     otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    ///     This method catches any exceptions thrown during deserialization and returns <c>false</c> in such cases.
    /// </remarks>
    public static bool TryDeserialize<T>(
        [StringSyntax(StringSyntaxAttribute.Json)]
        this string json,
        out T? result,
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
    ///     Attempts to deserialize the JSON data from the specified UTF-8 encoded stream into an instance of type
    ///     <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type into which to deserialize the JSON.</typeparam>
    /// <param name="utf8Json">A stream containing UTF-8 encoded JSON data.</param>
    /// <param name="result">
    ///     When this method returns, contains the deserialized object of type <typeparamref name="T" />,
    ///     if the deserialization succeeded, or the default value of <typeparamref name="T" /> if it failed.
    /// </param>
    /// <param name="options">
    ///     The <see cref="JsonSerializerOptions" /> to use during deserialization. If <c>null</c>,
    ///     <see cref="JsonSerializerOptions.Web" /> is used.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the JSON stream was successfully deserialized into an instance of <typeparamref name="T" />;
    ///     otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    ///     This method catches any exceptions thrown during deserialization and returns <c>false</c> in such cases.
    /// </remarks>
    public static bool TryDeserialize<T>(
        this Stream utf8Json,
        [NotNullWhen(true)] out T? result,
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