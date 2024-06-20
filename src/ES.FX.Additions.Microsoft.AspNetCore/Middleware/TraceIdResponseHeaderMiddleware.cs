using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace ES.FX.Additions.Microsoft.AspNetCore.Middleware;

/// <summary>
///     Middleware that sets the X-Trace-Id header with the current Activity ID or the TraceIdentifier of the request
/// </summary>
public class TraceIdResponseHeaderMiddleware(RequestDelegate next)
{
    [PublicAPI]
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = Activity.Current?.Id ?? context.TraceIdentifier;
            return Task.CompletedTask;
        });
        await next(context);
    }
}