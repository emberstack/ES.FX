using System.Diagnostics;
using System.Globalization;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace ES.FX.Additions.Microsoft.AspNetCore.Middleware;

/// <summary>
///     Middleware that sets the Server-Timing header with the total duration of the request
/// </summary>
public class ServerTimingMiddleware(RequestDelegate next)
{
    /// <summary>
    ///     Processes the request and registers a callback that writes the <c>Server-Timing</c> header with the
    ///     total request duration when the response starts
    /// </summary>
    /// <param name="context">The <see cref="HttpContext" /> for the current request</param>
    /// <returns>A <see cref="Task" /> that represents the completion of request processing</returns>
    [PublicAPI]
    public async Task InvokeAsync(HttpContext context)
    {
        var totalStopwatch = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            totalStopwatch.Stop();
            context.Response.Headers["Server-Timing"] = string.Create(CultureInfo.InvariantCulture,
                $"total;dur={totalStopwatch.Elapsed.TotalMilliseconds:0.0###}");
            return Task.CompletedTask;
        });
        await next(context);
    }
}