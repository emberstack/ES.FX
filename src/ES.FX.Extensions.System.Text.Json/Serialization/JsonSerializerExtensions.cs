using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace ES.FX.Extensions.System.Text.Json.Serialization;

/// <summary>
///     Provides extension methods for JSON deserialization using <see cref="JsonSerializer" />.
/// </summary>
[PublicAPI]
public static class JsonSerializerExtensions
{
    private static JsonSerializerOptions _defaultSerializerOptions = JsonSerializerExtendedOptions.WebApi;

    /// <summary>
    ///     Attempts to deserialize the specified JSON string into an instance of type <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type into which to deserialize the JSON data.</typeparam>
    /// <param name="utf8Json">A JSON string to deserialize.</param>
    /// <param name="result">
    ///     When this method returns, contains the deserialized object of type <typeparamref name="T" />,
    ///     if the deserialization succeeded; otherwise, the default value of <typeparamref name="T" />.
    /// </param>
    /// <param name="options">
    ///     The <see cref="JsonSerializerOptions" /> to use during deserialization. If <c>null</c>,
    ///     <see cref="JsonSerializerExtendedOptions.WebApi" /> is used.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the JSON string was successfully deserialized into an instance of <typeparamref name="T" />;
    ///     otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    ///     This method catches any exceptions thrown during deserialization and returns <c>false</c> in such cases.
    /// </remarks>
    public static bool TryJsonDeserialize<T>(
        [StringSyntax(StringSyntaxAttribute.Json)]
        this string? utf8Json,
        [NotNullWhen(true)] out T? result,
        JsonSerializerOptions? options = null)
    {
        if (utf8Json is null)
        {
            result = default;
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize<T>(utf8Json, options ?? _defaultSerializerOptions);
            return result is not null;
        }
        catch (Exception)
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    ///     Attempts to deserialize JSON data from the specified UTF-8 encoded stream into an instance of type
    ///     <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">The type into which to deserialize the JSON data.</typeparam>
    /// <param name="utf8Json">A stream containing UTF-8 encoded JSON data.</param>
    /// <param name="result">
    ///     When this method returns, contains the deserialized object of type <typeparamref name="T" />,
    ///     if the deserialization succeeded; otherwise, the default value of <typeparamref name="T" />.
    /// </param>
    /// <param name="options">
    ///     The <see cref="JsonSerializerOptions" /> to use during deserialization. If <c>null</c>,
    ///     <see cref="JsonSerializerExtendedOptions.WebApi" /> is used.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the JSON stream was successfully deserialized into an instance of <typeparamref name="T" />;
    ///     otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    ///     This method catches any exceptions thrown during deserialization and returns <c>false</c> in such cases.
    /// </remarks>
    public static bool TryJsonDeserialize<T>(
        this Stream? utf8Json,
        [NotNullWhen(true)] out T? result,
        JsonSerializerOptions? options = null)
    {
        if (utf8Json is null)
        {
            result = default;
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize<T>(utf8Json, options ?? _defaultSerializerOptions);
            return result is not null;
        }
        catch (Exception)
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    ///     Deserializes the specified JSON string into an instance of type <typeparamref name="T" />,
    ///     or returns the specified default value if deserialization fails.
    /// </summary>
    /// <typeparam name="T">The type into which to deserialize the JSON data.</typeparam>
    /// <param name="utf8Json">A JSON string to deserialize.</param>
    /// <param name="defaultValue">
    ///     The default value to return if deserialization fails.
    /// </param>
    /// <param name="options">
    ///     The <see cref="JsonSerializerOptions" /> to use during deserialization. If <c>null</c>,
    ///     <see cref="JsonSerializerExtendedOptions.WebApi" /> is used.
    /// </param>
    /// <returns>
    ///     The deserialized object of type <typeparamref name="T" />, or <paramref name="defaultValue" />
    ///     if deserialization fails.
    /// </returns>
    [return: NotNullIfNotNull(nameof(defaultValue))]
    public static T? JsonDeserializeOrDefault<T>(
        this string? utf8Json,
        T? defaultValue = default,
        JsonSerializerOptions? options = null)
    {
        if (utf8Json is null) return defaultValue;

        try
        {
            return JsonSerializer.Deserialize<T>(utf8Json, options ?? _defaultSerializerOptions) ?? defaultValue;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    /// <summary>
    ///     Deserializes JSON data from the specified UTF-8 encoded stream into an instance of type <typeparamref name="T" />,
    ///     or returns the specified default value if deserialization fails.
    /// </summary>
    /// <typeparam name="T">The type into which to deserialize the JSON data.</typeparam>
    /// <param name="utf8Json">A stream containing UTF-8 encoded JSON data.</param>
    /// <param name="defaultValue">
    ///     The default value to return if deserialization fails.
    /// </param>
    /// <param name="options">
    ///     The <see cref="JsonSerializerOptions" /> to use during deserialization. If <c>null</c>,
    ///     <see cref="JsonSerializerExtendedOptions.WebApi" /> is used.
    /// </param>
    /// <returns>
    ///     The deserialized object of type <typeparamref name="T" />, or <paramref name="defaultValue" />
    ///     if deserialization fails.
    /// </returns>
    [return: NotNullIfNotNull(nameof(defaultValue))]
    public static T? JsonDeserializeOrDefault<T>(
        this Stream? utf8Json,
        T? defaultValue = default,
        JsonSerializerOptions? options = null)
    {
        if (utf8Json is null) return defaultValue;

        try
        {
            return JsonSerializer.Deserialize<T>(utf8Json, options ?? _defaultSerializerOptions) ?? defaultValue;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }


    /// <summary>
    ///     Converts the specified object to an instance of type <typeparamref name="T" /> by serializing it to JSON
    ///     and then deserializing it as <typeparamref name="T" />. If the source is <c>null</c> or conversion fails,
    ///     returns the specified default value.
    /// </summary>
    /// <typeparam name="T">The target type into which to convert the object.</typeparam>
    /// <param name="source">
    ///     The object to convert. If <c>null</c>, the method returns <paramref name="defaultValue" />.
    /// </param>
    /// <param name="defaultValue">
    ///     The default value to return if conversion fails or if the source is <c>null</c>.
    /// </param>
    /// <param name="options">
    ///     The <see cref="JsonSerializerOptions" /> to use during serialization and deserialization.
    ///     If <c>null</c>, the default options (<see cref="_defaultSerializerOptions" />) are used.
    /// </param>
    /// <returns>
    ///     An instance of type <typeparamref name="T" /> converted from the source object, or <paramref name="defaultValue" />
    ///     if conversion fails or the source is <c>null</c>.
    /// </returns>
    /// <remarks>
    ///     This method performs a deep conversion by serializing the source object to JSON and then deserializing it
    ///     into the target type. It is useful for converting between types that have compatible JSON representations.
    /// </remarks>
    [return: NotNullIfNotNull(nameof(defaultValue))]
    public static T? ConvertViaJson<T>(
        this object? source,
        T? defaultValue = default,
        JsonSerializerOptions? options = null)
    {
        if (source is null) return defaultValue;

        try
        {
            var json = source as string ?? JsonSerializer.Serialize(source, options ?? _defaultSerializerOptions);
            return JsonSerializer.Deserialize<T>(json, options ?? _defaultSerializerOptions) ?? defaultValue;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }


    /// <summary>
    /// Attempts to convert the specified object to an instance of type <typeparamref name="T"/> by serializing it to JSON
    /// and then deserializing it as <typeparamref name="T"/>. If the conversion succeeds, the result is output;
    /// otherwise, the method returns <c>false</c> and the result is set to the default value.
    /// </summary>
    /// <typeparam name="T">The target type into which to convert the object.</typeparam>
    /// <param name="source">
    /// The object to convert. If <c>null</c>, the method returns <c>false</c> and <paramref name="result"/> is set to the default value.
    /// </param>
    /// <param name="result">
    /// When this method returns, contains the deserialized object of type <typeparamref name="T"/>,
    /// if the conversion succeeded; otherwise, the default value of <typeparamref name="T"/>.
    /// </param>
    /// <param name="options">
    /// The <see cref="JsonSerializerOptions"/> to use during serialization and deserialization.
    /// If <c>null</c>, the default options (<see cref="_defaultSerializerOptions"/>) are used.
    /// </param>
    /// <returns>
    /// <c>true</c> if the object was successfully converted to an instance of <typeparamref name="T"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method performs a deep conversion by serializing the source object to JSON and then deserializing it into the target type.
    /// If the source object is already a JSON string, that string is used directly.
    /// </remarks>
    public static bool TryConvertViaJson<T>(
        this object? source,
        [NotNullWhen(true)] out T? result,
        JsonSerializerOptions? options = null)
    {
        if (source is null)
        {
            result = default;
            return false;
        }

        try
        {
            var json = source as string ?? JsonSerializer.Serialize(source, options ?? _defaultSerializerOptions);
            result = JsonSerializer.Deserialize<T>(json, options ?? _defaultSerializerOptions);
            return result is not null;
        }
        catch (Exception)
        {
            result = default;
            return false;
        }
    }
}