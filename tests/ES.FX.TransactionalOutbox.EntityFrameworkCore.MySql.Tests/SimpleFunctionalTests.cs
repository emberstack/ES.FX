using System.Text.Json;
using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Extensions;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Testcontainers.MariaDb;
using Xunit;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.MySql.Tests;

public class SimpleFunctionalTests : IAsyncLifetime
{
    private string? _connectionString;
    private MariaDbContainer? _mariaDbContainer;

    public async Task InitializeAsync()
    {
        // Create a dedicated MariaDB container for this test
        _mariaDbContainer = new MariaDbBuilder()
            .WithImage("mariadb:latest")
            .Build();

        await _mariaDbContainer.StartAsync();
        _connectionString = _mariaDbContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_mariaDbContainer != null) await _mariaDbContainer.DisposeAsync();
    }

    [Fact]
    public Task UseMySqlOutboxProvider_Should_Configure_Provider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<OutboxTestDbContext>(options =>
        {
            options.UseMySql(_connectionString!, ServerVersion.AutoDetect(_connectionString!),
                o => o.MigrationsAssembly(typeof(SimpleFunctionalTests).Assembly.FullName));
            options.UseOutbox();
        });

        services.AddOutboxDeliveryService<OutboxTestDbContext, TestMessageHandler>(options =>
        {
            options.UseMySqlOutboxProvider();
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var outboxOptions =
            serviceProvider.GetRequiredService<IOptionsMonitor<OutboxDeliveryOptions<OutboxTestDbContext>>>();
        var options = outboxOptions.CurrentValue;

        // Assert
        Assert.NotNull(options.OutboxProvider);
        Assert.IsType<MySqlOutboxProvider<OutboxTestDbContext>>(options.OutboxProvider);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task MySqlProvider_Should_Lock_And_Return_Single_Outbox()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<OutboxTestDbContext>(options =>
        {
            options.UseMySql(_connectionString!, ServerVersion.AutoDetect(_connectionString!),
                o => o.MigrationsAssembly(typeof(SimpleFunctionalTests).Assembly.FullName));
            options.UseOutbox();
        });

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
        await context.Database.EnsureCreatedAsync();

        // Add test data
        var testMessage = new TestOrder { OrderNumber = $"TEST-{testId}-001", Amount = 100m };
        context.AddOutboxMessage(testMessage);
        await context.SaveChangesAsync();

        var provider = new MySqlOutboxProvider<OutboxTestDbContext>();

        // Act
        var outbox = await provider.GetNextExclusiveOutboxWithoutDelay(context);

        // Assert
        Assert.NotNull(outbox);

        // Check the related message
        var message = await context.Set<OutboxMessage>()
            .FirstOrDefaultAsync(m => m.OutboxId == outbox.Id);
        Assert.NotNull(message);

        var deserializedOrder = JsonSerializer.Deserialize<TestOrder>(message.Payload);
        Assert.NotNull(deserializedOrder);
        Assert.Equal($"TEST-{testId}-001", deserializedOrder.OrderNumber);
    }

    [Fact]
    public async Task MySqlProvider_Should_Prevent_Concurrent_Access()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<OutboxTestDbContext>(options =>
        {
            options.UseMySql(_connectionString!, ServerVersion.AutoDetect(_connectionString!),
                o => o.MigrationsAssembly(typeof(SimpleFunctionalTests).Assembly.FullName));
            options.UseOutbox();
        }, ServiceLifetime.Transient);

        var serviceProvider = services.BuildServiceProvider();

        // Setup initial data
        using (var scope = serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
            await context.Database.EnsureCreatedAsync();

            context.AddOutboxMessage(new TestOrder { OrderNumber = $"TEST-{testId}-001", Amount = 100m });
            await context.SaveChangesAsync();
        }

        var provider = new MySqlOutboxProvider<OutboxTestDbContext>();

        // Act - simulate concurrent access
        var tasks = new List<Task<Outbox?>>();
        for (var i = 0; i < 5; i++)
            tasks.Add(Task.Run(async () =>
            {
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
                await using var transaction = await context.Database.BeginTransactionAsync();
                var result = await provider.GetNextExclusiveOutboxWithoutDelay(context);
                if (result != null)
                {
                    // Lock the outbox to simulate what the delivery service does
                    result.Lock = Guid.NewGuid();
                    context.Update(result);
                    await context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return result;
            }));

        var results = await Task.WhenAll(tasks);

        // Assert - only one should succeed, others should get null
        var nonNullResults = results.Where(r => r != null).ToList();
        Assert.Single(nonNullResults);
    }

    [Fact]
    public async Task MySqlProvider_Should_Skip_Locked_Outboxes()
    {
        // Arrange
        var testId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<OutboxTestDbContext>(options =>
        {
            options.UseMySql(_connectionString!, ServerVersion.AutoDetect(_connectionString!),
                o => o.MigrationsAssembly(typeof(SimpleFunctionalTests).Assembly.FullName));
            options.UseOutbox();
        });

        services.AddOutboxDeliveryService<OutboxTestDbContext, TestMessageHandler>(options =>
        {
            options.UseMySqlOutboxProvider();
            options.BatchSize = 1;
        });

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxTestDbContext>();
        await context.Database.EnsureCreatedAsync();

        // Clean up any existing outboxes to ensure test isolation
        await context.Set<OutboxMessage>().ExecuteDeleteAsync();
        await context.Set<Outbox>().ExecuteDeleteAsync();

        // Add multiple outboxes - each SaveChanges creates a separate outbox
        context.AddOutboxMessage(new TestOrder { OrderNumber = $"TEST-{testId}-001", Amount = 100m });
        await context.SaveChangesAsync();

        context.AddOutboxMessage(new TestOrder { OrderNumber = $"TEST-{testId}-002", Amount = 200m });
        await context.SaveChangesAsync();

        context.AddOutboxMessage(new TestOrder { OrderNumber = $"TEST-{testId}-003", Amount = 300m });
        await context.SaveChangesAsync();

        // Lock the first outbox
        var firstOutbox = await context.Set<Outbox>()
            .OrderBy(o => o.AddedAt)
            .FirstAsync();
        firstOutbox.Lock = Guid.NewGuid();
        await context.SaveChangesAsync();


        var provider = new MySqlOutboxProvider<OutboxTestDbContext>();

        // Act
        await using var transaction = await context.Database.BeginTransactionAsync();
        var nextOutbox = await provider.GetNextExclusiveOutboxWithoutDelay(context);
        await transaction.CommitAsync();

        // Assert
        Assert.NotNull(nextOutbox);
        Assert.NotEqual(firstOutbox.Id, nextOutbox.Id);

        // Check the related message
        var message = await context.Set<OutboxMessage>()
            .FirstOrDefaultAsync(m => m.OutboxId == nextOutbox.Id);
        Assert.NotNull(message);

        var deserializedOrder = JsonSerializer.Deserialize<TestOrder>(message.Payload);
        Assert.NotNull(deserializedOrder);
        Assert.Equal($"TEST-{testId}-002", deserializedOrder.OrderNumber);
    }

    private class TestMessageHandler : IOutboxMessageHandler
    {
        public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}