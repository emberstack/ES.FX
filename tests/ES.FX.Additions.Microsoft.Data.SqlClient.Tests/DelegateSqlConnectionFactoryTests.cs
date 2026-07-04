using System.Data;
using ES.FX.Additions.Microsoft.Data.SqlClient.Abstractions;
using ES.FX.Additions.Microsoft.Data.SqlClient.Factories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Additions.Microsoft.Data.SqlClient.Tests;

public class DelegateSqlConnectionFactoryTests
{
    private const string ConnectionString =
        "Server=tcp:localhost,1433;Database=Test;User ID=sa;Password=Passw0rd!;TrustServerCertificate=True";

    [Fact]
    public void Constructor_NullServiceProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DelegateSqlConnectionFactory(null!, _ => new SqlConnection()));
    }

    [Fact]
    public void Constructor_NullFactory_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        Assert.Throws<ArgumentNullException>(() =>
            new DelegateSqlConnectionFactory(provider, null!));
    }

    [Fact]
    public void Factory_ImplementsInterface()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var factory = new DelegateSqlConnectionFactory(provider, _ => new SqlConnection());
        Assert.IsType<ISqlConnectionFactory>(factory, false);
    }

    [Fact]
    public void CreateConnection_InvokesDelegate_ReturningConfiguredConnection()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var factory = new DelegateSqlConnectionFactory(provider,
            _ => new SqlConnection(ConnectionString));

        using var connection = factory.CreateConnection();

        Assert.NotNull(connection);
        // Constructing a SqlConnection does not open it; state must be Closed and no server is contacted.
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal("Test", connection.Database);
    }

    [Fact]
    public void CreateConnection_PassesTheProvidedServiceProviderToTheDelegate()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        IServiceProvider? captured = null;
        var factory = new DelegateSqlConnectionFactory(provider, sp =>
        {
            captured = sp;
            return new SqlConnection();
        });

        using var connection = factory.CreateConnection();

        Assert.Same(provider, captured);
    }

    [Fact]
    public void CreateConnection_ResolvesDependenciesFromTheServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ConnectionStringHolder(ConnectionString));
        using var provider = services.BuildServiceProvider();

        var factory = new DelegateSqlConnectionFactory(provider,
            sp => new SqlConnection(sp.GetRequiredService<ConnectionStringHolder>().Value));

        using var connection = factory.CreateConnection();

        Assert.Equal("Test", connection.Database);
    }

    [Fact]
    public void CreateConnection_InvokesDelegateOncePerCall_ReturningDistinctInstances()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var calls = 0;
        var factory = new DelegateSqlConnectionFactory(provider, _ =>
        {
            calls++;
            return new SqlConnection();
        });

        using var first = factory.CreateConnection();
        using var second = factory.CreateConnection();

        Assert.Equal(2, calls);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task CreateConnectionAsync_DefaultInterfaceMethod_ReturnsResultOfCreateConnection()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        ISqlConnectionFactory factory = new DelegateSqlConnectionFactory(provider,
            _ => new SqlConnection(ConnectionString));

        using var connection =
            await factory.CreateConnectionAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(connection);
        Assert.Equal("Test", connection.Database);
    }

    [Fact]
    public async Task CreateConnectionAsync_WithCancelledToken_ThrowsAndDoesNotInvokeDelegate()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var invoked = false;
        ISqlConnectionFactory factory = new DelegateSqlConnectionFactory(provider, _ =>
        {
            invoked = true;
            return new SqlConnection();
        });

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            factory.CreateConnectionAsync(cts.Token));
        Assert.False(invoked);
    }

    [Fact]
    public void CanRegisterAndResolveFactoryThroughDependencyInjection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISqlConnectionFactory>(sp =>
            new DelegateSqlConnectionFactory(sp, _ => new SqlConnection(ConnectionString)));
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<ISqlConnectionFactory>();
        using var connection = factory.CreateConnection();

        Assert.IsType<DelegateSqlConnectionFactory>(factory);
        Assert.Equal("Test", connection.Database);
    }

    private sealed record ConnectionStringHolder(string Value);
}