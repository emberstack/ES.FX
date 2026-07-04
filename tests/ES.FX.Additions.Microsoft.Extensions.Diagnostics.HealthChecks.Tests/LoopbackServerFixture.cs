using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks.Tests;

/// <summary>
///     Spins up a real Kestrel server bound to a loopback address on an OS-assigned free port. The
///     <see cref="Http.HttpGetHealthCheck" /> probes over a static, socket-based <c>HttpClient</c>, so an in-memory
///     <c>TestServer</c> is not reachable by it — a real listening endpoint is required. The server exposes a handful of
///     deterministic endpoints used by the functional tests.
/// </summary>
public sealed class LoopbackServerFixture : IAsyncLifetime
{
    private WebApplication? _app;

    /// <summary>Base address of the running loopback server, e.g. <c>http://127.0.0.1:5xxxx</c>.</summary>
    public string BaseAddress { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();

        var app = builder.Build();

        // Bind to loopback on an ephemeral port (":0" lets the OS pick a free port).
        app.Urls.Add("http://127.0.0.1:0");

        // 200 OK
        app.MapGet("/ok", () => Results.Ok("healthy"));

        // 201 Created — still a success status code.
        app.MapGet("/created", () => Results.Created("/created", null));

        // 404 Not Found — a non-success status code.
        app.MapGet("/missing", () => Results.NotFound());

        // 500 Internal Server Error — a non-success status code.
        app.MapGet("/error", () => Results.StatusCode(StatusCodes.Status500InternalServerError));

        // Slow endpoint used to trigger the per-attempt timeout. Honors the request-abort token so the
        // connection is torn down promptly when the client cancels.
        app.MapGet("/slow", async (HttpContext ctx) =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ctx.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                // Client went away — expected for the timeout/cancellation tests.
            }

            return Results.Ok();
        });

        await app.StartAsync();
        _app = app;

        var address = app.Urls.First();
        BaseAddress = address.TrimEnd('/');
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    /// <summary>Returns an absolute URI on the loopback server for the given relative path.</summary>
    public string Url(string path) => $"{BaseAddress}{path}";
}