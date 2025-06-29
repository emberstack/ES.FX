using ES.FX.TransactionalOutbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Playground.Microservice.Api.Host.Testing;
using Playground.Shared.Data.Simple.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore.Entities;

#pragma warning disable CS9113 // Parameter is unread.

namespace Playground.Microservice.Api.Host.HostedServices;

internal class TestHostedService(
    ILogger<TestHostedService> logger,
    IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;

        while (true)
        {
            var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<SimpleDbContext>>();

            await using var dbContext =
                await dbContextFactory.CreateDbContextAsync(stoppingToken).ConfigureAwait(false);
            for (var i = 0; i < 50; i++)
            {
                dbContext.AddOutboxMessage(new OutboxTestMessage
                {
                    SomeProp = "Property"
                });
                dbContext.SimpleUsers.Add(new SimpleUser
                    { Id = Guid.CreateVersion7(), Username = Guid.CreateVersion7().ToString() });
            }

            await dbContext.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
            //for (var i = 0; i < 50; i++)
            //{
            //    var redisMultiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
            //    var redisDatabase = redisMultiplexer.GetDatabase();
            //    await redisDatabase.StringGetAsync("something");
            //}


            await Task.Delay(15000, stoppingToken).ConfigureAwait(false);
        }
    }
}