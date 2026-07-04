using ES.FX.Zendesk.MCP.Host.Configuration;
using Microsoft.Extensions.Options;

namespace ES.FX.Zendesk.MCP.Host.Hosting;

/// <summary>
///     Rejects requests to the MCP endpoints that carry an <c>Origin</c> header not present in
///     <see cref="McpOptions.AllowedOrigins" />. The MCP Streamable HTTP transport specification requires servers
///     to validate the <c>Origin</c> header on all incoming connections to prevent DNS-rebinding attacks; the MCP
///     SDK does not implement this itself. Requests without an <c>Origin</c> header (non-browser clients such as
///     MCP agents and CLIs) always pass through.
/// </summary>
/// <remarks>
///     The endpoint prefix is frozen at pipeline-build time (matching the route <c>MapMcp</c> was mapped at,
///     which never changes after startup); only <see cref="McpOptions.AllowedOrigins" /> honors live
///     configuration reload.
/// </remarks>
internal sealed class McpOriginValidationMiddleware(
    RequestDelegate next,
    IOptionsMonitor<McpOptions> options,
    string endpoint)
{
    private readonly string _endpointPrefix = "/" + endpoint.Trim('/');

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsMcpRequest(context) || IsAllowedOrigin(context, options.CurrentValue))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Origin not allowed.");
    }

    private bool IsMcpRequest(HttpContext context)
    {
        // The endpoint is the route pattern MapMcp is mapped at ("" = application root, which makes every
        // path an MCP path — the health endpoints tolerate the Origin check because non-browser monitors
        // never send an Origin header).
        return _endpointPrefix == "/" || context.Request.Path.StartsWithSegments(_endpointPrefix);
    }

    private static bool IsAllowedOrigin(HttpContext context, McpOptions options)
    {
        var origin = context.Request.Headers.Origin;
        if (origin.Count == 0) return true;

        // Every Origin value (browsers send exactly one) must be allowlisted.
        foreach (var value in origin)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var normalized = value.TrimEnd('/');
            var allowed = options.AllowedOrigins.Any(allowedOrigin =>
                string.Equals(allowedOrigin.TrimEnd('/'), normalized, StringComparison.OrdinalIgnoreCase));
            if (!allowed) return false;
        }

        return true;
    }
}
