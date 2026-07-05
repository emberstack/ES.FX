using System.Reflection;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     Registration-time gate that decides which tool classes to register based on the configured
///     <c>Mcp:Tools:Areas</c> set. An empty set registers everything (backward compatible). The gate composes
///     with the read-only execution baseline in <c>Program.cs</c> via AND: read-only drops the write classes,
///     the area gate drops classes outside the configured areas.
/// </summary>
/// <remarks>
///     Fail-closed like the execution-mode resolver: any configured area that is not a real area (derived from
///     the tool classes actually present in the assembly) throws at construction with a message listing the
///     valid area names, rather than silently registering an empty or partial surface.
/// </remarks>
public sealed class ZendeskToolAreaGate
{
    private readonly HashSet<string> _selectedAreas;

    private ZendeskToolAreaGate(HashSet<string> selectedAreas) => _selectedAreas = selectedAreas;

    /// <summary>Whether area gating is active (a non-empty area set was configured).</summary>
    public bool IsActive => _selectedAreas.Count > 0;

    /// <summary>
    ///     Builds a gate from the configured area names, validating them against the areas that actually exist in
    ///     the given assembly.
    /// </summary>
    /// <param name="configuredAreas">The raw <c>Mcp:Tools:Areas</c> values (case-insensitive, may be empty).</param>
    /// <param name="assembly">The assembly to scan for <see cref="McpServerToolTypeAttribute" /> classes.</param>
    /// <returns>A gate that admits the configured areas (or all areas when none were configured).</returns>
    /// <exception cref="InvalidOperationException">
    ///     A configured area does not match any real tool area — the message lists the valid area names.
    /// </exception>
    public static ZendeskToolAreaGate FromConfiguration(IEnumerable<string>? configuredAreas, Assembly assembly)
    {
        var configured = (configuredAreas ?? []).ToList();
        var requested = configured
            .Where(area => !string.IsNullOrWhiteSpace(area))
            .Select(area => area.Trim())
            .ToList();

        if (requested.Count == 0)
        {
            // Absent or empty configuration means "all areas" (backward compatible). But a configuration
            // that is present yet entirely blank (e.g. Mcp__Tools__Areas__0= with no value) is almost
            // certainly a fat-fingered attempt to restrict the surface — fail closed rather than silently
            // exposing the full tool set, mirroring the execution-mode resolver's fail-closed posture.
            if (configured.Count > 0)
                throw new InvalidOperationException(
                    "Mcp:Tools:Areas was configured but contains no non-blank area names. Remove the setting " +
                    "entirely to expose all areas, or list at least one valid area.");

            return new ZendeskToolAreaGate(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var validAreas = ToolTypes(assembly)
            .Select(ZendeskToolArea.OfType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknown = requested
            .Where(area => !validAreas.Contains(area))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"Mcp:Tools:Areas contains unknown area(s): {string.Join(", ", unknown)}. " +
                $"Valid areas are: {string.Join(", ", validAreas.OrderBy(a => a, StringComparer.Ordinal))}.");

        return new ZendeskToolAreaGate(new HashSet<string>(requested, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Whether the tool class of the given type should be registered under this gate.
    /// </summary>
    /// <typeparam name="TTool">The <see cref="McpServerToolTypeAttribute" /> class.</typeparam>
    /// <returns><c>true</c> when the gate is inactive (all areas) or the class's area is selected.</returns>
    public bool Allows<TTool>() => !IsActive || _selectedAreas.Contains(ZendeskToolArea.OfType(typeof(TTool)));

    /// <summary>Enumerates the <see cref="McpServerToolTypeAttribute" /> classes declared in an assembly.</summary>
    private static IEnumerable<Type> ToolTypes(Assembly assembly) =>
        assembly.GetTypes().Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null);
}
