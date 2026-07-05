using System.Reflection;
using System.Runtime.CompilerServices;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     Pins the documented Hermes/Claude include-list <em>profiles</em> — ready-made <c>tools.include</c> sets a
///     user can copy-paste — against the live tool metadata, so the docs profiles can never silently drift from
///     the real tool surface. Each profile is a sorted, newline-separated list of tool names committed under
///     <c>Profiles/</c>.
/// </summary>
/// <remarks>
///     <para>
///         Profiles asserted here:
///         <list type="bullet">
///             <item><c>all-read-tools.include.txt</c> — every ReadOnly tool.</item>
///             <item><c>tickets.include.txt</c> — every tool in the <c>tickets</c> area.</item>
///             <item><c>tickets-read.include.txt</c> — every ReadOnly tool in the <c>tickets</c> area.</item>
///         </list>
///     </para>
///     <para>
///         REGENERATION: if the tool surface changes on purpose, set the environment variable
///         <c>REGENERATE_TOOL_PROFILES=1</c> and run these tests once. Each test then rewrites its committed
///         snapshot file (in the source tree, located via <see cref="SourceProfilesDirectory" />) with the
///         current set and fails with a "regenerated" message; commit the updated files and re-run without the
///         variable to confirm green.
///     </para>
/// </remarks>
public class ZendeskToolProfileSnapshotTests
{
    private const string RegenerateEnvVar = "REGENERATE_TOOL_PROFILES";

    /// <summary>Every declared tool name paired with its ReadOnly flag and derived area.</summary>
    private static IEnumerable<(string Name, bool ReadOnly, string Area)> DeclaredTools() =>
        typeof(Program).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(attribute => attribute is not null && !string.IsNullOrWhiteSpace(attribute.Name))
            .Select(attribute => (attribute!.Name!, attribute.ReadOnly, ZendeskToolArea.OfToolName(attribute.Name!)));

    [Fact]
    public void All_Read_Tools_Profile_Matches_The_Committed_Snapshot() =>
        AssertProfile("all-read-tools.include.txt",
            DeclaredTools().Where(tool => tool.ReadOnly).Select(tool => tool.Name));

    [Fact]
    public void Tickets_Area_Profile_Matches_The_Committed_Snapshot() =>
        AssertProfile("tickets.include.txt",
            DeclaredTools().Where(tool => tool.Area == "tickets").Select(tool => tool.Name));

    [Fact]
    public void Tickets_Read_Profile_Matches_The_Committed_Snapshot() =>
        AssertProfile("tickets-read.include.txt",
            DeclaredTools().Where(tool => tool is { ReadOnly: true, Area: "tickets" }).Select(tool => tool.Name));

    /// <summary>
    ///     Asserts the sorted, newline-terminated <paramref name="expected" /> set equals the committed snapshot
    ///     file. When regeneration is requested, rewrites the source file and fails with a clear message.
    /// </summary>
    private static void AssertProfile(string fileName, IEnumerable<string> expected)
    {
        var current = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var content = string.Join('\n', current) + "\n";

        if (Environment.GetEnvironmentVariable(RegenerateEnvVar) == "1")
        {
            var sourcePath = Path.Combine(SourceProfilesDirectory(), fileName);
            File.WriteAllText(sourcePath, content);
            Assert.Fail(
                $"Regenerated {fileName} ({current.Length} tools) at {sourcePath}. " +
                $"Commit the file and re-run without {RegenerateEnvVar}=1.");
        }

        // Read the committed bytes (copied next to the test assembly). Normalize CRLF so a checkout with
        // autocrlf cannot make the test fail on line endings alone.
        var committedPath = Path.Combine(AppContext.BaseDirectory, "Profiles", fileName);
        Assert.True(File.Exists(committedPath),
            $"Missing committed profile '{fileName}'. Set {RegenerateEnvVar}=1 and re-run to generate it.");

        var committed = File.ReadAllText(committedPath).Replace("\r\n", "\n");
        var committedNames = committed.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Two focused assertions so a drift reports the exact additions/removals rather than a byte blob diff.
        Assert.Empty(current.Except(committedNames)); // tools present now but missing from the snapshot
        Assert.Empty(committedNames.Except(current)); // stale names in the snapshot no longer produced
        Assert.Equal(content, committed); // and the exact sorted, newline-terminated shape
    }

    /// <summary>
    ///     The <c>Profiles/</c> directory in the source tree, resolved from this file's compile-time path so
    ///     regeneration writes the committed files rather than the build-output copies.
    /// </summary>
    private static string SourceProfilesDirectory([CallerFilePath] string thisFilePath = "") =>
        Path.Combine(Path.GetDirectoryName(thisFilePath)!, "..", "Profiles");
}