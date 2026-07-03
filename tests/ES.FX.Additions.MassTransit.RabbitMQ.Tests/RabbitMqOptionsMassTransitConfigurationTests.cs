using ES.FX.Additions.MassTransit.RabbitMQ.Configuration;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Additions.MassTransit.RabbitMQ.Tests;

/// <summary>
///     Functional coverage that <see cref="RabbitMqOptions" /> can be bound from configuration and used to drive a
///     real MassTransit RabbitMQ host configuration against an in-memory <see cref="IServiceCollection" />. No broker
///     is contacted: the bus is registered and its configuration realized, but never started/connected.
/// </summary>
public class RabbitMqOptionsMassTransitConfigurationTests
{
    private static RabbitMqOptions BindOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:Host"] = "rabbitmq://broker.internal/vhost",
                ["RabbitMq:Username"] = "svc-account",
                ["RabbitMq:Password"] = "p@ss"
            })
            .Build();

        var options = configuration.GetSection("RabbitMq").Get<RabbitMqOptions>();
        Assert.NotNull(options);
        return options;
    }

    [Fact]
    public async Task Options_Drive_MassTransit_RabbitMq_Host_Configuration_And_Register_The_Bus()
    {
        var options = BindOptions();

        string? capturedUsername = null;
        string? capturedPassword = null;

        var services = new ServiceCollection();
        services.AddMassTransit(x =>
            x.UsingRabbitMq((_, cfg) =>
                cfg.Host(options.Host, h =>
                {
                    // Prove the bound options actually flow into the transport host configurator.
                    h.Username(options.Username!);
                    capturedUsername = options.Username;
                    h.Password(options.Password!);
                    capturedPassword = options.Password;
                })));

        // MassTransit registers IAsyncDisposable-only services, so the provider must be disposed asynchronously.
        await using var provider = services.BuildServiceProvider();

        // The bus factory ran the host configuration (delegates above executed) during resolution.
        var busControl = provider.GetRequiredService<IBusControl>();
        Assert.NotNull(busControl);
        Assert.NotNull(provider.GetRequiredService<IBus>());
        Assert.NotNull(provider.GetRequiredService<ISendEndpointProvider>());

        Assert.Equal("svc-account", capturedUsername);
        Assert.Equal("p@ss", capturedPassword);

        // The configured host address is derived from the bound Host value (proves it was consumed, not ignored).
        Assert.Equal("broker.internal", busControl.Address.Host);
        Assert.Contains("vhost", busControl.Address.AbsolutePath);
    }

    [Fact]
    public async Task Host_Value_Is_Honored_When_It_Differs()
    {
        // Regression guard: a change that hard-codes or drops the Host would surface here.
        var options = new RabbitMqOptions { Host = "rabbitmq://other-host/" };

        var services = new ServiceCollection();
        services.AddMassTransit(x =>
            x.UsingRabbitMq((_, cfg) => cfg.Host(options.Host)));

        await using var provider = services.BuildServiceProvider();
        var busControl = provider.GetRequiredService<IBusControl>();

        Assert.Equal("other-host", busControl.Address.Host);
    }

    [Fact]
    public async Task Missing_Credentials_Still_Produce_A_Valid_Bus_Registration()
    {
        // Host-only configuration (null Username/Password) must not throw at configuration time.
        var options = new RabbitMqOptions { Host = "rabbitmq://anon-host/" };

        var services = new ServiceCollection();
        services.AddMassTransit(x =>
            x.UsingRabbitMq((_, cfg) =>
                cfg.Host(options.Host, h =>
                {
                    if (options.Username is not null) h.Username(options.Username);
                    if (options.Password is not null) h.Password(options.Password);
                })));

        await using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IBusControl>());
    }
}
