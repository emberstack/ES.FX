using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace ES.FX.Microsoft.AspNetCore.Middleware;

/// <summary>
///     Middleware that sets the Server-Timing header with the total duration of the request
/// </summary>
public class ServerTimingMiddleware(RequestDelegate next)
{
    [PublicAPI]
    public async Task InvokeAsync(HttpContext context)
    {
        var totalStopwatch = Stopwatch.StartNew();
        await next(context);
        totalStopwatch.Stop();
        context.Response.Headers["Server-Timing"] = $"total;dur={totalStopwatch.Elapsed.TotalMilliseconds}";
    }
}