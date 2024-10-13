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
        await next(context);
        context.Response.Headers.Add("X-Trace-Id", context.TraceIdentifier);
    }
}