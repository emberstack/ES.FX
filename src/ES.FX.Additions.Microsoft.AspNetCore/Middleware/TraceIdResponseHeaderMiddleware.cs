using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace ES.FX.Additions.Microsoft.AspNetCore.Middleware;

/// <summary>
///     Middleware that sets the X-Trace-Id header with the current Activity ID or the TraceIdentifier of the request
/// </summary>
public class TraceIdResponseHeaderMiddleware(RequestDelegate next)
{
    /// <summary>
    ///     Processes the request and registers a callback that writes the <c>X-Trace-Id</c> header with the
    ///     current <see cref="Activity" /> ID (falling back to <see cref="HttpContext.TraceIdentifier" />) when
    ///     the response starts
    /// </summary>
    /// <param name="context">The <see cref="HttpContext" /> for the current request</param>
    /// <returns>A <see cref="Task" /> that represents the completion of request processing</returns>
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