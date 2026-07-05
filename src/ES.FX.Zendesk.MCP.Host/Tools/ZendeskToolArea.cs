using System.Reflection;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     Derives the <em>area</em> (top-level Zendesk resource grouping) of an MCP tool from its snake_case name,
///     the single source of truth used by both the registration-time area gate in <c>Program.cs</c> and the
///     tests. The area is the resource the tool operates on — e.g. <c>tickets_get</c> and
///     <c>tickets_search_export</c> are both in the <c>tickets</c> area, <c>ticket_fields_list</c> is in
///     <c>ticket_fields</c>.
/// </summary>
/// <remarks>
///     Rule: the area is the longest multi-word area prefix a name starts with, otherwise its first
///     underscore-delimited segment. Because a name's first segment(s) are always the (plural) area, this is a
///     pure function of the name — no class-name coupling. Each <see cref="McpServerToolTypeAttribute" /> class is
///     expected to be area-homogeneous (all its tools share one area); <see cref="OfType" /> asserts that.
/// </remarks>
public static class ZendeskToolArea
{
    /// <summary>
    ///     Multi-word areas whose names contain an underscore. These must be matched before the single-segment
    ///     fallback so that, for example, <c>ticket_fields_get</c> resolves to <c>ticket_fields</c> and not
    ///     <c>ticket</c>. Ordered longest-first is unnecessary because no entry is a prefix of another.
    /// </summary>
    private static readonly string[] MultiWordAreas =
        ["ticket_fields", "custom_statuses", "job_statuses", "suspended_tickets"];

    /// <summary>
    ///     Returns the area of a single tool name (e.g. <c>tickets</c> for <c>tickets_search_export</c>).
    /// </summary>
    /// <param name="toolName">The snake_case MCP tool name.</param>
    /// <returns>The area segment.</returns>
    /// <exception cref="ArgumentException">The name is null, empty, or whitespace.</exception>
    public static string OfToolName(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Tool name must be a non-empty snake_case string.", nameof(toolName));

        foreach (var area in MultiWordAreas)
            if (toolName == area || toolName.StartsWith(area + "_", StringComparison.Ordinal))
                return area;

        var underscore = toolName.IndexOf('_');
        return underscore < 0 ? toolName : toolName[..underscore];
    }

    /// <summary>
    ///     Returns the (single) area of a <see cref="McpServerToolTypeAttribute" /> tool class by inspecting its
    ///     declared tools. Every tool class is area-homogeneous; if a class ever declares tools spanning more than
    ///     one area this throws, surfacing the inconsistency at startup rather than silently mis-gating.
    /// </summary>
    /// <param name="toolType">A class annotated with <see cref="McpServerToolTypeAttribute" />.</param>
    /// <returns>The area shared by all tools declared on the class.</returns>
    /// <exception cref="InvalidOperationException">
    ///     The class declares no named tools, or declares tools spanning more than one area.
    /// </exception>
    public static string OfType(Type toolType)
    {
        var areas = ToolNames(toolType).Select(OfToolName).Distinct().ToList();

        return areas.Count switch
        {
            0 => throw new InvalidOperationException(
                $"Tool class '{toolType.Name}' declares no [McpServerTool] with a Name — cannot derive its area."),
            1 => areas[0],
            _ => throw new InvalidOperationException(
                $"Tool class '{toolType.Name}' declares tools spanning multiple areas ({string.Join(", ", areas)}). " +
                "A tool class must be area-homogeneous so area gating can register or drop it as a unit.")
        };
    }

    /// <summary>
    ///     Enumerates the declared MCP tool names on a <see cref="McpServerToolTypeAttribute" /> class.
    /// </summary>
    /// <param name="toolType">A class annotated with <see cref="McpServerToolTypeAttribute" />.</param>
    /// <returns>The non-null tool names declared directly on the class.</returns>
    public static IEnumerable<string> ToolNames(Type toolType) =>
        toolType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!);
}
