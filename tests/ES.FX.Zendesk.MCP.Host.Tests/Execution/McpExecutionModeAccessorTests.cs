using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using Microsoft.AspNetCore.Http;

namespace ES.FX.Zendesk.MCP.Host.Tests.Execution;

public class McpExecutionModeAccessorTests
{
    private static McpExecutionModeAccessor CreateAccessor(McpOptions options, string? headerValue)
    {
        var context = new DefaultHttpContext();
        if (headerValue is not null) context.Request.Headers[options.Execution.HeaderName] = headerValue;

        var httpContextAccessor = new HttpContextAccessor { HttpContext = context };
        return new McpExecutionModeAccessor(httpContextAccessor, new StaticOptionsMonitor<McpOptions>(options));
    }

    [Fact]
    public void EffectiveMode_Uses_Baseline_When_No_Header()
    {
        var accessor = CreateAccessor(new McpOptions { Execution = { Mode = McpExecutionMode.DryRun } }, null);

        Assert.Equal(McpExecutionMode.DryRun, accessor.ConfiguredMode);
        Assert.Equal(McpExecutionMode.DryRun, accessor.EffectiveMode);
    }

    [Fact]
    public void EffectiveMode_Header_Can_Tighten()
    {
        var accessor = CreateAccessor(new McpOptions { Execution = { Mode = McpExecutionMode.Default } }, "read-only");

        Assert.Equal(McpExecutionMode.ReadOnly, accessor.EffectiveMode);
    }

    [Fact]
    public void EffectiveMode_Header_Cannot_Relax()
    {
        var accessor = CreateAccessor(new McpOptions { Execution = { Mode = McpExecutionMode.ReadOnly } }, "default");

        Assert.Equal(McpExecutionMode.ReadOnly, accessor.EffectiveMode);
    }

    [Fact]
    public void EffectiveMode_Respects_Custom_Header_Name()
    {
        var accessor = CreateAccessor(
            new McpOptions { Execution = { Mode = McpExecutionMode.Default, HeaderName = "X-Custom-Mode" } },
            "dry-run");

        Assert.Equal(McpExecutionMode.DryRun, accessor.EffectiveMode);
    }

    [Fact]
    public void EffectiveMode_Ignores_Header_When_Override_Disabled()
    {
        var accessor = CreateAccessor(
            new McpOptions { Execution = { Mode = McpExecutionMode.Default, AllowHeaderOverride = false } },
            "read-only");

        Assert.Equal(McpExecutionMode.Default, accessor.EffectiveMode);
    }
}