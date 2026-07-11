using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace ES.FX.OpenData.Romania.TerritorialUnits.Internal;

/// <summary>Reads this package's embedded SIRUTA resource: a gzip-compressed delimited CSV.</summary>
internal static class OpenDataResources
{
    /// <summary>
    ///     Streams the rows of a gzip-compressed delimited embedded resource, each split into fields. Decompresses
    ///     and reads line-by-line; no whole-file string is materialized.
    /// </summary>
    public static IEnumerable<string[]> ReadGzipDelimitedLines(
        Assembly assembly, string logicalName, char delimiter, bool skipHeader)
    {
        using var stream = Open(assembly, logicalName);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8, true);
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

    private static Stream Open(Assembly assembly, string logicalName) =>
        assembly.GetManifestResourceStream(logicalName)
        ?? throw new InvalidOperationException(
            $"Embedded resource '{logicalName}' was not found in assembly '{assembly.GetName().Name}'.");
}