using System.Net.Sockets;
using ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks.Tests;

/// <summary>
///     Functional regression coverage for <see cref="HttpGetHealthCheck" /> and
///     <see cref="HttpGetHealthCheckOptions" />. These probe a real loopback Kestrel server (no Docker) so behavior
///     is exercised end-to-end over the socket-based static <c>HttpClient</c>.
/// </summary>
public sealed class HttpGetHealthCheckTests(LoopbackServerFixture server) : IClassFixture<LoopbackServerFixture>
{
    private static HealthCheckContext ContextWith(HealthStatus failureStatus, IHealthCheck instance)
    {
        var registration = new HealthCheckRegistration(
            "test",
            instance,
            failureStatus,
            tags: null);

        return new HealthCheckContext { Registration = registration };
    }

    private static async Task<HealthCheckResult> RunAsync(
        HttpGetHealthCheckOptions options,
        HealthStatus failureStatus = HealthStatus.Unhealthy,
        CancellationToken? cancellationToken = null)
    {
        var check = new HttpGetHealthCheck(options);
        var context = ContextWith(failureStatus, check);
        return await check.CheckHealthAsync(context,
            cancellationToken ?? TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Returns_Healthy_For_A_200_Response()
    {
        var result = await RunAsync(new HttpGetHealthCheckOptions { Uri = server.Url("/ok") });

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task Returns_Healthy_For_A_201_Created_Response()
    {
        // Any 2xx is a success status code, not just 200.
        var result = await RunAsync(new HttpGetHealthCheckOptions { Uri = server.Url("/created") });

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Returns_Registered_FailureStatus_For_A_404_Response()
    {
        var result = await RunAsync(new HttpGetHealthCheckOptions { Uri = server.Url("/missing") });

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Description);
        Assert.Contains("404", result.Description);
    }

    [Fact]
    public async Task Non_Success_Status_Includes_The_Numeric_Status_Code_In_The_Description()
    {
        var result = await RunAsync(new HttpGetHealthCheckOptions { Uri = server.Url("/error") });

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("HTTP GET returned 500", result.Description);
    }

    [Fact]
    public async Task Non_Success_Status_Honors_A_Custom_FailureStatus()
    {
        // The check must report the registration's configured FailureStatus, not a hardcoded Unhealthy.
        var result = await RunAsync(
            new HttpGetHealthCheckOptions { Uri = server.Url("/missing") },
            HealthStatus.Degraded);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task Non_Success_Status_Carries_No_Exception()
    {
        var result = await RunAsync(new HttpGetHealthCheckOptions { Uri = server.Url("/missing") });

        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task Returns_FailureStatus_When_The_Connection_Cannot_Be_Established()
    {
        // Grab a free port, then don't listen on it — GetAsync should fail with an HttpRequestException,
        // which the check translates into the registered failure status (with the exception attached).
        var deadPort = GetFreePort();
        var result = await RunAsync(
            new HttpGetHealthCheckOptions { Uri = $"http://127.0.0.1:{deadPort}/" },
            HealthStatus.Unhealthy);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task Connection_Failure_Honors_A_Custom_FailureStatus()
    {
        var deadPort = GetFreePort();
        var result = await RunAsync(
            new HttpGetHealthCheckOptions { Uri = $"http://127.0.0.1:{deadPort}/" },
            HealthStatus.Degraded);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task Per_Attempt_Timeout_Reports_FailureStatus()
    {
        // The endpoint sleeps far longer than the timeout; the per-attempt timeout must cancel the request
        // and surface the failure status rather than throwing to the caller.
        var result = await RunAsync(new HttpGetHealthCheckOptions
        {
            Uri = server.Url("/slow"),
            Timeout = TimeSpan.FromMilliseconds(250)
        });

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task Per_Attempt_Timeout_Honors_A_Custom_FailureStatus()
    {
        var result = await RunAsync(
            new HttpGetHealthCheckOptions { Uri = server.Url("/slow"), Timeout = TimeSpan.FromMilliseconds(250) },
            HealthStatus.Degraded);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task Ambient_Cancellation_Propagates_To_The_Caller()
    {
        // When the ambient token (not the per-attempt timeout) is cancelled, the exception filter does NOT
        // swallow it — cancellation must propagate as an OperationCanceledException.
        using var cts = new CancellationTokenSource();
        // ReSharper disable once MethodSupportsCancellation
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await RunAsync(
                new HttpGetHealthCheckOptions { Uri = server.Url("/slow") },
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Already_Cancelled_Ambient_Token_Propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await RunAsync(
                new HttpGetHealthCheckOptions { Uri = server.Url("/ok") },
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Ambient_Cancellation_With_A_Configured_Timeout_Still_Propagates_To_The_Caller()
    {
        // Closes the "linked-CTS" gap: when BOTH a per-attempt Timeout is set (so the request runs on a
        // linked token) AND the ambient token is cancelled mid-flight, the exception filter guards on the
        // AMBIENT token's IsCancellationRequested — not the linked/timeout token — so cancellation must still
        // propagate rather than being mislabeled as a timeout failure. The timeout here is long enough that
        // the ambient cancellation wins the race.
        using var cts = new CancellationTokenSource();
        // ReSharper disable once MethodSupportsCancellation
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await RunAsync(
                new HttpGetHealthCheckOptions { Uri = server.Url("/slow"), Timeout = TimeSpan.FromSeconds(30) },
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Already_Cancelled_Ambient_Token_With_A_Configured_Timeout_Propagates()
    {
        // Same guard, deterministic variant: the ambient token is already cancelled before the call. Even
        // though a Timeout is configured (linked CTS present), the ambient cancellation must propagate.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await RunAsync(
                new HttpGetHealthCheckOptions { Uri = server.Url("/ok"), Timeout = TimeSpan.FromSeconds(30) },
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Per_Attempt_Timeout_Fires_While_Ambient_Token_Stays_Live_Reports_FailureStatus()
    {
        // The other side of the linked-CTS branch: the timeout CTS cancels the request but the AMBIENT token
        // is never cancelled, so cancellationToken.IsCancellationRequested is false and the filter swallows
        // the TaskCanceledException into the registered failure status instead of propagating it.
        using var ambient = new CancellationTokenSource();

        var result = await RunAsync(
            new HttpGetHealthCheckOptions { Uri = server.Url("/slow"), Timeout = TimeSpan.FromMilliseconds(250) },
            HealthStatus.Degraded,
            ambient.Token);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotNull(result.Exception);
        Assert.False(ambient.IsCancellationRequested);
    }

    [Fact]
    public async Task Relative_Uri_Throws_Uncaught_InvalidOperationException()
    {
        // Pins current real behavior (NOT a bug fix): Uri is passed straight to HttpClient.GetAsync as a
        // string. A relative URI produces an InvalidOperationException, which is neither HttpRequestException
        // nor TaskCanceledException, so the exception filter does NOT catch it and it bubbles out of the
        // health check rather than reporting FailureStatus.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await RunAsync(new HttpGetHealthCheckOptions { Uri = "/relative/path" }));
    }

    [Fact]
    public async Task Garbage_Non_Uri_Throws_Uncaught_InvalidOperationException()
    {
        // A malformed, non-absolute URI string is treated the same as a relative one by HttpClient: an
        // uncaught InvalidOperationException, not a FailureStatus result.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await RunAsync(new HttpGetHealthCheckOptions { Uri = "not a valid uri" }));
    }

    [Fact]
    public async Task Unsupported_Scheme_Uri_Throws_Uncaught_NotSupportedException()
    {
        // An absolute URI with a scheme HttpClient does not support (e.g. ftp) throws NotSupportedException,
        // which is likewise outside the exception filter and bubbles out rather than reporting FailureStatus.
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await RunAsync(new HttpGetHealthCheckOptions { Uri = "ftp://example.test/resource" }));
    }

    [Fact]
    public void Constructor_Throws_On_Null_Options()
    {
        Assert.Throws<ArgumentNullException>(() => new HttpGetHealthCheck(null!));
    }

    [Fact]
    public async Task Check_Is_Reusable_Across_Multiple_Invocations()
    {
        // The static shared HttpClient must not leave the instance in a broken state between calls.
        var check = new HttpGetHealthCheck(new HttpGetHealthCheckOptions { Uri = server.Url("/ok") });

        for (var i = 0; i < 3; i++)
        {
            var context = ContextWith(HealthStatus.Unhealthy, check);
            var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);
            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
    }

    [Fact]
    public void Options_Timeout_Defaults_To_Null()
    {
        var options = new HttpGetHealthCheckOptions { Uri = "http://example.test/" };

        Assert.Null(options.Timeout);
        Assert.Equal("http://example.test/", options.Uri);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
