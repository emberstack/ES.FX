using System.Reflection;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     Shared naming vocabulary for the tool-naming invariants, kept in one place so the sweep test and any
///     future consumers agree on what counts as a read verb or a destructive verb.
/// </summary>
internal static class ZendeskToolNaming
{
    /// <summary>
    ///     The controlled read-verb set. A tool name is a <em>read</em> name iff one of its underscore-delimited
    ///     segments is one of these verbs (the verb may be followed by qualifier segments such as
    ///     <c>_many</c>, <c>_active</c>, <c>_by_external_id</c>, <c>_incremental</c>). Every other name is a write.
    /// </summary>
    internal static readonly string[] ReadVerbs = ["get", "list", "search", "count", "export", "autocomplete"];

    /// <summary>
    ///     The destructive verbs, expressed as segment sequences so a verb is matched on word boundaries — never
    ///     as a substring. <c>merge</c> the verb (<c>tickets_merge</c>) is destructive; <c>merges</c> the noun
    ///     (<c>organizations_merges_get</c>, a read) is not.
    /// </summary>
    internal static readonly string[][] DestructiveVerbs =
        [["delete"], ["merge"], ["redact"], ["mark", "spam"]];

    /// <summary>Whether the name is a read name under the controlled read-verb rule.</summary>
    internal static bool IsReadName(string name) =>
        name.Split('_').Any(segment => ReadVerbs.Contains(segment));

    /// <summary>Whether the name contains a destructive verb as a whole segment (or segment sequence).</summary>
    internal static bool ContainsDestructiveVerb(string name)
    {
        var segments = name.Split('_');
        return DestructiveVerbs.Any(verb =>
        {
            for (var start = 0; start + verb.Length <= segments.Length; start++)
                if (segments.Skip(start).Take(verb.Length).SequenceEqual(verb))
                    return true;
            return false;
        });
    }
}

/// <summary>
///     Reflection sweep over every <see cref="McpServerToolTypeAttribute" /> class in the host assembly,
///     enforcing the annotation conventions the execution-mode security model relies on: the read/write split
///     encoded in the class name must match the <see cref="McpServerToolAttribute.ReadOnly" /> annotation
///     (Program.cs gates write-tool registration on it), destructively named tools must be annotated as
///     destructive, and the tool totals are pinned as a drift guard so a new tool cannot land without
///     deliberately updating the expected counts.
/// </summary>
public class ZendeskToolAnnotationSweepTests
{
    private const int ExpectedTotalTools = 168;
    private const int ExpectedReadTools = 81;
    private const int ExpectedWriteTools = 87;

    private static List<(Type Type, MethodInfo Method, McpServerToolAttribute Attribute)> DeclaredTools()
    {
        var tools = typeof(Program).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                            BindingFlags.DeclaredOnly)
                .Select(method => (Type: type, Method: method,
                    Attribute: method.GetCustomAttribute<McpServerToolAttribute>()))
                .Where(tool => tool.Attribute is not null))
            .Select(tool => (tool.Type, tool.Method, Attribute: tool.Attribute!))
            .ToList();

        Assert.NotEmpty(tools);
        Assert.All(tools, tool => Assert.False(string.IsNullOrWhiteSpace(tool.Attribute.Name),
            $"{tool.Type.Name}.{tool.Method.Name}: every [McpServerTool] must set an explicit snake_case Name."));
        return tools;
    }

    private static bool IsWriteToolsClass(Type type) =>
        type.Name.EndsWith("WriteTools", StringComparison.Ordinal);

    [Fact]
    public void Write_Tool_Classes_Declare_Only_Write_Tools()
    {
        // A ReadOnly=true tool on a *WriteTools class would be dropped by Program.cs's ReadOnly-baseline
        // registration gate for no reason — and signals a mislabeled write operation.
        var offenders = DeclaredTools()
            .Where(tool => IsWriteToolsClass(tool.Type) && tool.Attribute.ReadOnly)
            .Select(tool => $"{tool.Type.Name}.{tool.Method.Name} ({tool.Attribute.Name})")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Read_Tool_Classes_Declare_Only_ReadOnly_Tools()
    {
        // A ReadOnly=false tool on a read tools class would stay registered under a ReadOnly baseline while
        // bypassing the ZendeskToolInvoker write gate — a mutating tool must live on a *WriteTools class.
        var offenders = DeclaredTools()
            .Where(tool => !IsWriteToolsClass(tool.Type) && !tool.Attribute.ReadOnly)
            .Select(tool => $"{tool.Type.Name}.{tool.Method.Name} ({tool.Attribute.Name})")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Destructively_Named_Tools_Are_Annotated_Destructive()
    {
        string[] destructiveMarkers = ["delete", "destroy", "redact"];

        var offenders = DeclaredTools()
            .Where(tool => destructiveMarkers.Any(marker =>
                tool.Attribute.Name!.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Where(tool => !tool.Attribute.Destructive)
            .Select(tool => $"{tool.Type.Name}.{tool.Method.Name} ({tool.Attribute.Name})")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Read_Verb_Suffix_Matches_ReadOnly_Annotation()
    {
        // THE naming invariant: a tool name is a read name (some segment is one of get|list|search|count|export|
        // autocomplete, optionally + qualifier segments) IFF ReadOnly=true. This is the machine guarantee that
        // the resource-first names are honest — the read/write split is legible from the name alone, without
        // consulting annotations (which MCP clients like Hermes never see). A failure here is a NAMING bug from
        // the rename: fix the tool name (or move the tool to the correct read/write class), never weaken this.
        var offenders = DeclaredTools()
            .Where(tool => ZendeskToolNaming.IsReadName(tool.Attribute.Name!) != tool.Attribute.ReadOnly)
            .Select(tool =>
                $"{tool.Type.Name}.{tool.Method.Name} ({tool.Attribute.Name}): " +
                $"IsReadName={ZendeskToolNaming.IsReadName(tool.Attribute.Name!)} but ReadOnly={tool.Attribute.ReadOnly}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Destructive_Verb_In_Name_Implies_Destructive_Annotation()
    {
        // A name containing a destructive verb (delete|merge|redact|mark_spam, matched on word boundaries so
        // organizations_merges_get — a read — is NOT flagged) must carry Destructive=true. The converse is not
        // required: a tool may be destructive without one of these verbs (e.g. tickets_comments_make_private).
        var offenders = DeclaredTools()
            .Where(tool => ZendeskToolNaming.ContainsDestructiveVerb(tool.Attribute.Name!) &&
                           !tool.Attribute.Destructive)
            .Select(tool => $"{tool.Type.Name}.{tool.Method.Name} ({tool.Attribute.Name})")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Tool_Totals_Match_The_Pinned_Drift_Guard()
    {
        // Deliberate pin: adding or removing a tool must consciously update these counts (and the docs).
        var tools = DeclaredTools();

        Assert.Equal(ExpectedTotalTools, tools.Count);
        Assert.Equal(ExpectedReadTools, tools.Count(tool => tool.Attribute.ReadOnly));
        Assert.Equal(ExpectedWriteTools, tools.Count(tool => !tool.Attribute.ReadOnly));
    }
}
