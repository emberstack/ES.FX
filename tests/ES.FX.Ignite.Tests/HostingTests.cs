using ES.FX.Ignite.Configuration;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Tests;

public class HostingTests
{
    [Fact]
    public void Ignite_Should_be_allowed_once()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Ignite();

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.Ignite());
    }

    [Fact]
    public void Ignite_WhenCalled_ShouldAddServices()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Ignite();

        var serviceProvider = builder.Build().Services;
        Assert.NotNull(serviceProvider.GetRequiredService<IgniteSettings>());
    }
}