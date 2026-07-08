using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using JetBrains.Annotations;

namespace ES.FX.OpenData;

/// <summary>
///     Helpers for reading a dataset's embedded resources. Datasets embed their data (edition-stamped) and load
///     it through these helpers, keeping every package self-contained and free of third-party parser
///     dependencies. Delimited files are read line-by-line so no whole-file string is ever allocated.
/// </summary>
[PublicAPI]
public static class OpenDataResources
{
    /// <summary>Opens an embedded resource stream by its logical name.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the resource is not found.</exception>
    public static Stream Open(Assembly assembly, string logicalName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(logicalName);
        return assembly.GetManifestResourceStream(logicalName)
               ?? throw new InvalidOperationException(
                   $"Embedded resource '{logicalName}' was not found in assembly '{assembly.GetName().Name}'.");
    }

    /// <summary>
    ///     Streams the rows of a delimited (e.g. semicolon-separated) embedded resource, each split into fields.
    ///     Reads line-by-line; no whole-file string is materialized.
    /// </summary>
    public static IEnumerable<string[]> ReadDelimitedLines(
        Assembly assembly, string logicalName, char delimiter, bool skipHeader)
    {
        using var stream = Open(assembly, logicalName);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var first = true;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            if (first)
            {
                first = false;
                if (skipHeader) continue;
            }

            yield return line.Split(delimiter);
        }
    }

    /// <summary>
    ///     Streams the rows of a gzip-compressed delimited embedded resource, each split into fields. Decompresses
    ///     and reads line-by-line; no whole-file string is materialized.
    /// </summary>
    public static IEnumerable<string[]> ReadGzipDelimitedLines(
        Assembly assembly, string logicalName, char delimiter, bool skipHeader)
    {
        using var stream = Open(assembly, logicalName);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var first = true;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            if (first)
            {
                first = false;
                if (skipHeader) continue;
            }

            yield return line.Split(delimiter);
        }
    }

    /// <summary>Deserializes a JSON embedded resource using a source-generated <see cref="JsonTypeInfo{T}" /> (AOT/trim-safe).</summary>
    public static T DeserializeJson<T>(Assembly assembly, string logicalName, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        using var stream = Open(assembly, logicalName);
        return JsonSerializer.Deserialize(stream, typeInfo)
               ?? throw new InvalidOperationException($"Embedded JSON resource '{logicalName}' deserialized to null.");
    }
}
