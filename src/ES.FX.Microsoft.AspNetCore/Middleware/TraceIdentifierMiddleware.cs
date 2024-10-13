using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace ES.FX.Microsoft.AspNetCore.Middleware;

/// <summary>
///     Middleware that sets the X-Trace-Id header with the TraceIdentifier of the request
/// </summary>
public class TraceIdentifierMiddleware(RequestDelegate next)
{
    [PublicAPI]
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;
            return Task.CompletedTask;
        });
        await next(context);
    }
}