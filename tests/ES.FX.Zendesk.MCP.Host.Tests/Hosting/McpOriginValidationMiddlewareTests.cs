using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Hosting;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ES.FX.Zendesk.MCP.Host.Tests.Hosting;

public class McpOriginValidationMiddlewareTests
{
    private static Task<(HttpContext Context, bool NextCalled)> Invoke(McpOptions options, string? origin,
        string path = "/") =>
        InvokeWithOrigins(options, origin is null ? StringValues.Empty : new StringValues(origin), path);

    private static async Task<(HttpContext Context, bool NextCalled)> InvokeWithOrigins(McpOptions options,
        StringValues origin, string path = "/")
    {
        var nextCalled = false;
        var middleware = new McpOriginValidationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, new StaticOptionsMonitor<McpOptions>(options), options.Endpoint);

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (origin.Count > 0) context.Request.Headers.Origin = origin;

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
    public async Task Rejects_When_Any_Of_Multiple_Origin_Values_Is_Not_Allowlisted()
    {
        // A request smuggling a second, unlisted Origin value next to an allowlisted one must not pass:
        // EVERY value has to be on the allowlist.
        var options = new McpOptions { AllowedOrigins = ["https://ops.example"] };

        var (context, nextCalled) = await InvokeWithOrigins(options,
            new StringValues(["https://ops.example", "https://evil.example"]));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Skips_Empty_And_Whitespace_Origin_Values()
    {
        // Empty/whitespace values are not browser origins — they are skipped rather than matched against the
        // allowlist (which is empty here, so treating them as origins would reject the request with 403).
        var (context, nextCalled) = await InvokeWithOrigins(new McpOptions(), new StringValues([" ", ""]));

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
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
