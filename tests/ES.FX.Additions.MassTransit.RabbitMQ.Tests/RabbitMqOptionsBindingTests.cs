using ES.FX.Additions.MassTransit.RabbitMQ.Configuration;
using Microsoft.Extensions.Configuration;

namespace ES.FX.Additions.MassTransit.RabbitMQ.Tests;

/// <summary>
///     Functional regression coverage for <see cref="RabbitMqOptions" />. The type's documented purpose is to be
///     bound from configuration and passed into MassTransit's RabbitMQ host configuration, so these tests exercise
///     real <see cref="IConfiguration" /> binding rather than trivial property get/set.
/// </summary>
public class RabbitMqOptionsBindingTests
{
    private static IConfiguration BuildConfiguration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Defaults_Are_Empty_Host_And_Null_Credentials()
    {
        var options = new RabbitMqOptions();

        Assert.Equal(string.Empty, options.Host);
        Assert.Null(options.Username);
        Assert.Null(options.Password);
    }

    [Fact]
    public void Binds_All_Properties_From_Configuration_Section()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "rabbit.example.com",
            ["RabbitMq:Username"] = "guest-user",
            ["RabbitMq:Password"] = "s3cr3t"
        });

        var options = configuration.GetSection("RabbitMq").Get<RabbitMqOptions>();

        Assert.NotNull(options);
        Assert.Equal("rabbit.example.com", options.Host);
        Assert.Equal("guest-user", options.Username);
        Assert.Equal("s3cr3t", options.Password);
    }

    [Fact]
    public void Binds_Host_Only_And_Leaves_Credentials_Null()
    {
        // A common real-world case: broker allows anonymous/default access, so only the host is configured.
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "amqp://localhost"
        });

        var options = configuration.GetSection("RabbitMq").Get<RabbitMqOptions>();

        Assert.NotNull(options);
        Assert.Equal("amqp://localhost", options.Host);
        Assert.Null(options.Username);
        Assert.Null(options.Password);
    }

    [Fact]
    public void Binding_An_Absent_Section_Leaves_Defaults()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["SomethingElse:Value"] = "x"
        });

        // Binding onto an existing instance from a missing section must not clobber defaults.
        var options = new RabbitMqOptions();
        configuration.GetSection("RabbitMq").Bind(options);

        Assert.Equal(string.Empty, options.Host);
        Assert.Null(options.Username);
        Assert.Null(options.Password);
    }

    [Fact]
    public void Rebinding_A_Populated_Section_Overwrites_Present_Keys_And_Preserves_Absent_Ones()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "new-host",
            ["RabbitMq:Username"] = "new-user"
            // Password intentionally absent
        });

        var options = new RabbitMqOptions
        {
            Host = "old-host",
            Username = "old-user",
            Password = "kept-password"
        };

        configuration.GetSection("RabbitMq").Bind(options);

        Assert.Equal("new-host", options.Host);
        Assert.Equal("new-user", options.Username);
        // Config binder does not null-out properties whose key is absent from the source.
        Assert.Equal("kept-password", options.Password);
    }
}
