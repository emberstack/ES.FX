using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Additions.Microsoft.AspNetCore.Tests;

/// <summary>
///     Builds an in-memory <see cref="TestServer" /> pipeline around a single piece of middleware, with a
///     terminal endpoint that echoes back the request headers so tests can assert what the middleware produced.
/// </summary>
internal static class TestServerFactory
{
    /// <summary>
    ///     Header prefix used by the terminal endpoint to echo request headers that the middleware set/passed on.
    ///     A request header <c>Foo: bar</c> is echoed back as response header <c>Echo-Foo: bar</c>.
    /// </summary>
    public const string EchoResponsePrefix = "Echo-";

    public static TestServer CreateWithMiddleware<TMiddleware>(RequestDelegate? terminal = null)
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .Configure(app =>
                    {
                        app.UseMiddleware<TMiddleware>();
                        app.Run(terminal ?? EchoRequestHeaders);
                    });
            });

        var host = builder.Start();
        return host.GetTestServer();
    }

    /// <summary>
    ///     Terminal endpoint that copies every request header into a response header prefixed with
    ///     <see cref="EchoResponsePrefix" />, so tests can inspect what middleware placed on the request.
    /// </summary>
    private static Task EchoRequestHeaders(HttpContext context)
    {
        foreach (var header in context.Request.Headers)
            context.Response.Headers[EchoResponsePrefix + header.Key] = header.Value;

        return Task.CompletedTask;
    }
}