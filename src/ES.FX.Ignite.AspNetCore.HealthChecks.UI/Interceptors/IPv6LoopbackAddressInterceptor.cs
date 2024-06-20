using HealthChecks.UI.Core;
using HealthChecks.UI.Core.Extensions;
using HealthChecks.UI.Data;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ES.FX.Ignite.AspNetCore.HealthChecks.UI.Interceptors;

/// <summary>
///     This interceptor is used to fix relative health check addresses when using IPv6 loopback address.
/// </summary>
/// <param name="server"></param>
// ReSharper disable once InconsistentNaming
public class IPv6LoopbackAddressInterceptor(IServer server) : IHealthCheckCollectorInterceptor
{
    public ValueTask OnCollectExecuting(HealthCheckConfiguration configuration)
    {
        Uri.TryCreate(configuration.Uri, UriKind.Absolute, out var absoluteUri);

        if (absoluteUri == null || !absoluteUri.IsValidHealthCheckEndpoint())
        {
            var relativeUrl = configuration.Uri;

            var addressFeature = server.Features.Get<IServerAddressesFeature>();
            var serverAddress = addressFeature?.Addresses.FirstOrDefault();
            if (serverAddress is null) return ValueTask.CompletedTask;

            var serverAddressUriBuilder = new UriBuilder(serverAddress);
            if (serverAddressUriBuilder.Host.Contains("::"))
                serverAddressUriBuilder.Host = serverAddressUriBuilder.Host
                    .Replace("[::]", "[::1]");

            Uri.TryCreate(serverAddressUriBuilder.Uri, relativeUrl, out absoluteUri);
        }

        if (absoluteUri is not null) configuration.Uri = absoluteUri.ToString();

        return ValueTask.CompletedTask;
    }

    public ValueTask OnCollectExecuted(UIHealthReport report) => ValueTask.CompletedTask;
}