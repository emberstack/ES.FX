using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Migrations;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using ES.FX.Migrations.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests;

public class RelationalDbContextMigrationsTaskExtensionsTests
{
    private static IHostApplicationBuilder CreateBuilderWithContext()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddLogging();
        builder.Services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        return builder;
    }

    [Fact]
    public void AddDbContextMigrationsTask_RegistersMigrationsTaskOfExpectedType()
    {
        var builder = CreateBuilderWithContext();

        builder.AddDbContextMigrationsTask<TestDbContext>();

        using var provider = builder.Services.BuildServiceProvider();
        var tasks = provider.GetServices<IMigrationsTask>().ToArray();

        var task = Assert.Single(tasks);
        Assert.IsType<RelationalDbContextMigrationsTask<TestDbContext>>(task);
    }

    [Fact]
    public void AddDbContextMigrationsTask_IsRegisteredAsTransient()
    {
        var builder = CreateBuilderWithContext();

        builder.AddDbContextMigrationsTask<TestDbContext>();

        var descriptor = Assert.Single(builder.Services,
            d => d.ServiceType == typeof(IMigrationsTask));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);

        using var provider = builder.Services.BuildServiceProvider();
        // Transient => a fresh instance each resolve.
        Assert.NotSame(
            provider.GetRequiredService<IMigrationsTask>(),
            provider.GetRequiredService<IMigrationsTask>());
    }

    [Fact]
    public void AddDbContextMigrationsTask_CalledTwice_DoesNotDuplicateSameImplementation()
    {
        // TryAddEnumerable de-dupes identical (service, implementation) pairs.
        var builder = CreateBuilderWithContext();

        builder.AddDbContextMigrationsTask<TestDbContext>();
        builder.AddDbContextMigrationsTask<TestDbContext>();

        var count = builder.Services.Count(d =>
            d.ServiceType == typeof(IMigrationsTask) &&
            d.ImplementationType == typeof(RelationalDbContextMigrationsTask<TestDbContext>));
        Assert.Equal(1, count);
    }
}
