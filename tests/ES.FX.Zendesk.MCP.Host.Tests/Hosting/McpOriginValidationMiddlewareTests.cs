using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Hosting;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using Microsoft.AspNetCore.Http;

namespace ES.FX.Zendesk.MCP.Host.Tests.Hosting;

public class McpOriginValidationMiddlewareTests
{
    private static async Task<(HttpContext Context, bool NextCalled)> Invoke(McpOptions options, string? origin,
        string path = "/")
    {
        var nextCalled = false;
        var middleware = new McpOriginValidationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, new StaticOptionsMonitor<McpOptions>(options), options.Endpoint);

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (origin is not null) context.Request.Headers.Origin = origin;

        await middleware.InvokeAsync(context);
        return (context, nextCalled);
    }

    [Fact]
    public async Task Allows_Requests_Without_Origin_Header()
    {
        var (context, nextCalled) = await Invoke(new McpOptions(), origin: null);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Rejects_Browser_Origin_When_Allowlist_Is_Empty()
    {
        var (context, nextCalled) = await Invoke(new McpOptions(), "https://evil.example");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Allows_Allowlisted_Origin()
    {
        var options = new McpOptions { AllowedOrigins = ["https://ops.example"] };

        var (_, nextCalled) = await Invoke(options, "https://ops.example");

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("https://ops.example/", "https://ops.example")]
    [InlineData("https://ops.example", "https://ops.example/")]
    [InlineData("https://OPS.example", "https://ops.example")]
    public async Task Origin_Comparison_Normalizes_Case_And_Trailing_Slash(string allowed, string origin)
    {
        var options = new McpOptions { AllowedOrigins = [allowed] };

        var (_, nextCalled) = await Invoke(options, origin);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Rejects_Origin_Not_On_Allowlist()
    {
        var options = new McpOptions { AllowedOrigins = ["https://ops.example"] };

        var (context, nextCalled) = await Invoke(options, "https://evil.example");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Ignores_Origin_On_Paths_Outside_The_Mcp_Endpoint()
    {
        var options = new McpOptions { Endpoint = "mcp" };

        var (_, nextCalled) = await Invoke(options, "https://evil.example", "/health");

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Validates_Origin_On_The_Mcp_Endpoint_Path()
    {
        var options = new McpOptions { Endpoint = "mcp" };

        var (context, nextCalled) = await Invoke(options, "https://evil.example", "/mcp");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
}
