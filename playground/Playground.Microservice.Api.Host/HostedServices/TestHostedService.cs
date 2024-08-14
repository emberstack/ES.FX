#pragma warning disable CS9113 // Parameter is unread.

using StackExchange.Redis;

namespace Playground.Microservice.Api.Host.HostedServices;

internal class TestHostedService(
    ILogger<TestHostedService> logger,
    IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            await Task.Delay(2000);

            var multiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
            multiplexer.GetDatabase().StringSet("test", "test"u8.ToArray());
        }
    }
}