using System.Reflection;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

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
    public void Tool_Totals_Match_The_Pinned_Drift_Guard()
    {
        // Deliberate pin: adding or removing a tool must consciously update these counts (and the docs).
        var tools = DeclaredTools();

        Assert.Equal(ExpectedTotalTools, tools.Count);
        Assert.Equal(ExpectedReadTools, tools.Count(tool => tool.Attribute.ReadOnly));
        Assert.Equal(ExpectedWriteTools, tools.Count(tool => !tool.Attribute.ReadOnly));
    }
}
