#pragma warning disable CS9113 // Parameter is unread.

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore;

namespace Playground.Microservice.Api.Host.HostedServices;

internal class TestHostedService(
    ILogger<TestHostedService> logger,
    IServiceProvider serviceProvider,
    IDbContextFactory<SimpleDbContext> dbContextFactory,
    SimpleDbContext context,
    SqlConnection connection
) : BackgroundService
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

            var a = connection.State;
        }
    }
}