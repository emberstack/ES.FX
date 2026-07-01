using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks.Http;

/// <summary>
///     A diagnostic health check implementation that probes a URI using an HTTP GET request over a shared static
///     <see cref="HttpClient" />. The probe reads only the response headers
///     (<see cref="HttpCompletionOption.ResponseHeadersRead" />) and reports
///     <see cref="HealthStatus.Healthy" /> for a success status code. Non-success status codes, request failures and
///     per-attempt timeouts report the registration's configured
///     <see cref="HealthCheckRegistration.FailureStatus" />, while ambient cancellation is propagated to the caller.
/// </summary>
public class HttpGetHealthCheck(HttpGetHealthCheckOptions options) : IHealthCheck
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        ActivityHeadersPropagator = null,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    });

    private readonly HttpGetHealthCheckOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var timeout = _options.Timeout;
        using var timeoutCts = timeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        timeoutCts?.CancelAfter(timeout.GetValueOrDefault());

        var requestToken = timeoutCts?.Token ?? cancellationToken;

        try
        {
            using var response = await Client
                .GetAsync(_options.Uri, HttpCompletionOption.ResponseHeadersRead, requestToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus,
                    $"HTTP GET returned {(int)response.StatusCode}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: exception);
        }
    }
}