#pragma warning disable CS9113 // Parameter is unread.

using ES.FX.TransactionalOutbox.EntityFrameworkCore;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Messages;
using Microsoft.EntityFrameworkCore;
using Playground.Microservice.Api.Host.Testing;
using Playground.Shared.Data.Simple.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore.Entities;

namespace Playground.Microservice.Api.Host.HostedServices;

internal class TestHostedService(
    ILogger<TestHostedService> logger,
    IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;

        var factory = serviceProvider.GetRequiredService<IDbContextFactory<SimpleDbContext>>();
        while (true)
        {
            await using var dbContext = await factory.CreateDbContextAsync(stoppingToken).ConfigureAwait(false);
            for (var i = 0; i < 50; i++)
            {
                dbContext.AddOutboxMessage(new OutboxTestMessage("Property"), new OutboxMessageOptions
                {
                    MaxAttempts = 5,
                    DelayBetweenAttempts = 5,
                    DelayBetweenAttemptsIsExponential = true
                });
                dbContext.SimpleUsers.Add(new SimpleUser { Id = Guid.NewGuid(), Username = Guid.NewGuid().ToString() });
            }

            await dbContext.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(60_000, stoppingToken).ConfigureAwait(false);
        }
    }
}