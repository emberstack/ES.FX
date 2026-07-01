using ES.FX.Zendesk.MCP.Host.Execution;

namespace ES.FX.Zendesk.MCP.Host.Tests.Execution;

public class McpExecutionModeResolverTests
{
    [Theory]
    // A header may tighten the baseline...
    [InlineData(McpExecutionMode.Default, "read-only", true, McpExecutionMode.ReadOnly)]
    [InlineData(McpExecutionMode.Default, "dry-run", true, McpExecutionMode.DryRun)]
    [InlineData(McpExecutionMode.DryRun, "read-only", true, McpExecutionMode.ReadOnly)]
    // ...but never relax it (no privilege escalation via header).
    [InlineData(McpExecutionMode.ReadOnly, "default", true, McpExecutionMode.ReadOnly)]
    [InlineData(McpExecutionMode.ReadOnly, "dry-run", true, McpExecutionMode.ReadOnly)]
    [InlineData(McpExecutionMode.DryRun, "default", true, McpExecutionMode.DryRun)]
    // Override disabled => baseline wins regardless of the header.
    [InlineData(McpExecutionMode.DryRun, "read-only", false, McpExecutionMode.DryRun)]
    // Unknown / empty header => baseline.
    [InlineData(McpExecutionMode.DryRun, "garbage", true, McpExecutionMode.DryRun)]
    [InlineData(McpExecutionMode.Default, null, true, McpExecutionMode.Default)]
    [InlineData(McpExecutionMode.Default, "", true, McpExecutionMode.Default)]
    public void Resolves_Effective_Mode(McpExecutionMode configured, string? header, bool allowOverride,
        McpExecutionMode expected)
    {
        Assert.Equal(expected, McpExecutionModeResolver.Resolve(configured, header, allowOverride));
    }

    [Theory]
    [InlineData("readonly", McpExecutionMode.ReadOnly)]
    [InlineData("READ_ONLY", McpExecutionMode.ReadOnly)]
    [InlineData("read-only", McpExecutionMode.ReadOnly)]
    [InlineData("dryrun", McpExecutionMode.DryRun)]
    [InlineData("Dry-Run", McpExecutionMode.DryRun)]
    [InlineData("default", McpExecutionMode.Default)]
    [InlineData("normal", McpExecutionMode.Default)]
    public void Parses_Known_Values(string value, McpExecutionMode expected)
    {
        Assert.True(McpExecutionModeResolver.TryParse(value, out var mode));
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nope")]
    [InlineData(null)]
    public void Fails_To_Parse_Unknown_Values(string? value)
    {
        Assert.False(McpExecutionModeResolver.TryParse(value, out _));
    }
}