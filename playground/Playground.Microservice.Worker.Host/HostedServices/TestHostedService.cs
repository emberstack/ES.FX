#pragma warning disable CS9113 // Parameter is unread.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Playground.Shared.Data.Simple.EntityFrameworkCore;

namespace Playground.Microservice.Worker.Host.HostedServices;

internal class TestHostedService(
    ILogger<TestHostedService> logger,
    IServiceProvider serviceProvider,
    IDbContextFactory<SimpleDbContext> dbContextFactory,
    SimpleDbContext context) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            await Task.Delay(2000);

            logger.LogInformation("Running");

            var writeFactory = serviceProvider.GetRequiredService<IDbContextFactory<SimpleDbContext>>();

            var writeContext = writeFactory.CreateDbContext();

            var writeUsers = writeContext.SimpleUsers.ToList();

            var contextUsers = context.SimpleUsers.ToList();
        }
    }
}