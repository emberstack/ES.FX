using ES.FX.Ignite.Migrations.Configuration;
using ES.FX.Ignite.Migrations.Hosting;
using ES.FX.Ignite.Migrations.Service;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Migrations.Tests.Hosting;

public class MigrationsServiceHostingExtensionsTests
{
    [Fact]
    public void IgniteDoesNotAllowReconfiguration()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteMigrationsService();

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteMigrationsService());
    }

    [Fact]
    public void IgniteShouldAddTheServices()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteMigrationsService();

        var serviceProvider = builder.Build().Services;
        var migrationService = serviceProvider.GetRequiredService<IHostedService>();
        Assert.True(migrationService is MigrationsService);
        Assert.NotNull(serviceProvider.GetRequiredService<MigrationsServiceSparkSettings>());
    }
}