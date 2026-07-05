using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ES.FX.Zendesk.MCP.Host.Tests.Testing;

/// <summary>The vendored Zendesk OpenAPI document a schema lives in.</summary>
internal enum ZendeskOasDocument
{
    /// <summary><c>src/ES.FX.Zendesk/OpenApi/support-oas.yaml</c>.</summary>
    Support,

    /// <summary><c>src/ES.FX.Zendesk/OpenApi/helpcenter-oas.yaml</c>.</summary>
    HelpCenter
}

/// <summary>
///     A deliberately pragmatic line-scanner over the vendored Zendesk OpenAPI YAML (tens of thousands of lines;
///     no YAML dependency is welcome in this repo). It extracts the top-level property names of a
///     <c>components/schemas</c> schema, resolving one composition level: <c>allOf</c>/<c>anyOf</c>/<c>oneOf</c>
///     members that are <c>$ref</c>s (followed recursively) or inline objects (their own <c>properties</c>
///     block). It relies on the specs' completely regular indentation — schema names at 4 spaces,
///     schema-level keys at 6, property names at 8, composition members at 8 (<c>- </c>), inline-member property
///     names at 12 — and deliberately ignores anything nested deeper (sub-object properties, examples), which is
///     exactly what keeps <c>example:</c> maps and nested payloads out of the result.
/// </summary>
internal static class ZendeskOasSchemas
{
    private static readonly Lazy<string[]> SupportLines = new(() => Load("support-oas.yaml"));
    private static readonly Lazy<string[]> HelpCenterLines = new(() => Load("helpcenter-oas.yaml"));

    private static readonly Regex PropertyKeyAt8 = new(@"^ {8}([A-Za-z0-9_$-]+):", RegexOptions.Compiled);
    private static readonly Regex PropertyKeyAt12 = new(@"^ {12}([A-Za-z0-9_$-]+):", RegexOptions.Compiled);

    private static readonly Regex CompositionRef =
        new(@"^ {8}- \$ref: '#/components/schemas/(\w+)'", RegexOptions.Compiled);

    /// <summary>
    ///     Returns the top-level property names of <paramref name="schemaName" /> in
    ///     <paramref name="document" />. Throws when the schema cannot be found — a missing schema after a
    ///     re-vendor is exactly the drift these tests exist to catch.
    /// </summary>
    public static IReadOnlySet<string> PropertyNames(ZendeskOasDocument document, string schemaName)
    {
        var lines = document is ZendeskOasDocument.Support ? SupportLines.Value : HelpCenterLines.Value;
        var names = new HashSet<string>(StringComparer.Ordinal);
        Collect(lines, schemaName, names, 0);
        return names;
    }

    private static void Collect(string[] lines, string schemaName, HashSet<string> names, int depth)
    {
        // A $ref cycle or a composition tower deeper than the specs actually use — stop rather than recurse.
        if (depth > 4)
            throw new InvalidOperationException(
                $"Schema '{schemaName}' nests compositions deeper than expected — extend the scanner.");

        var (start, end) = SchemaBlock(lines, schemaName);
        var inComposition = false; // inside a schema-level allOf:/anyOf:/oneOf:
        var inInlineMember = false; // inside an inline (non-$ref) member of that composition

        for (var index = start; index < end; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var indent = line.Length - line.TrimStart().Length;

            if (indent <= 6)
            {
                inComposition = line is "      allOf:" or "      anyOf:" or "      oneOf:";
                inInlineMember = false;
                if (line == "      properties:")
                    index = CollectKeys(lines, index + 1, end, PropertyKeyAt8, 8, names) - 1;
            }
            else if (inComposition && indent == 8 && line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
            {
                if (CompositionRef.Match(line) is { Success: true } reference)
                {
                    Collect(lines, reference.Groups[1].Value, names, depth + 1);
                    inInlineMember = false;
                }
                else
                {
                    inInlineMember = true;
                }
            }
            else if (inInlineMember && line == "          properties:")
            {
                index = CollectKeys(lines, index + 1, end, PropertyKeyAt12, 12, names) - 1;
            }
        }
    }

    /// <summary>
    ///     Collects map keys at exactly <paramref name="keyIndent" /> until the block dedents below it,
    ///     returning the index of the first line after the block. Deeper lines (sub-schemas, multi-line
    ///     descriptions) are skipped, never collected.
    /// </summary>
    private static int CollectKeys(string[] lines, int start, int end, Regex keyPattern, int keyIndent,
        HashSet<string> names)
    {
        var index = start;
        for (; index < end; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Length - line.TrimStart().Length < keyIndent) break;
            if (keyPattern.Match(line) is { Success: true } key) names.Add(key.Groups[1].Value);
        }

        return index;
    }

    /// <summary>Locates the line range of a schema's body (schema names sit at 4-space indentation).</summary>
    private static (int Start, int End) SchemaBlock(string[] lines, string schemaName)
    {
        var header = $"    {schemaName}:";
        var start = Array.IndexOf(lines, header);
        if (start < 0)
            throw new InvalidOperationException(
                $"Schema '{schemaName}' was not found in the vendored OAS — if Zendesk renamed it, update the " +
                "entity-to-schema map in the OAS staleness tests.");

        var end = start + 1;
        while (end < lines.Length)
        {
            var line = lines[end];
            if (!string.IsNullOrWhiteSpace(line) && line.Length - line.TrimStart().Length <= 4) break;
            end++;
        }

        return (start + 1, end);
    }

    /// <summary>Loads a vendored spec relative to this source file (they are too large to copy to output).</summary>
    private static string[] Load(string fileName, [CallerFilePath] string thisFilePath = "")
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFilePath)!,
            "..", "..", "..", "src", "ES.FX.Zendesk", "OpenApi", fileName));
        Assert.True(File.Exists(path), $"Vendored OAS not found at {path}.");
        return File.ReadAllLines(path);
    }
}