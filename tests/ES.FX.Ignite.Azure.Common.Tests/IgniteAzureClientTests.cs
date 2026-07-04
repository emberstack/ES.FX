using Azure.Data.Tables;
using ES.FX.Ignite.Azure.Common.Hosting;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Ignite.Azure.Common.Tests;

/// <summary>
///     Functional coverage of
///     <see cref="AzureCommonHostingExtensions.IgniteAzureClient{TClient,TOptions}" />.
///     Uses the real <see cref="TableServiceClient" /> / <see cref="TableClientOptions" /> pair as a
///     concrete Azure client. No live Azure is contacted: registration and resolution only build the
///     client object; no network I/O occurs until an operation is invoked.
/// </summary>
public class IgniteAzureClientTests
{
    // A valid-looking (but fake) Azure Table Storage connection string. Never used for real I/O.
    private const string FakeConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;" +
        "AccountKey=AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=;" +
        "TableEndpoint=https://devstoreaccount1.table.core.windows.net/;";

    private static IConfigurationSection BuildClientConfigurationSection(
        string sectionName = "client",
        string? connectionString = FakeConnectionString)
    {
        var data = new Dictionary<string, string?>();
        if (connectionString is not null)
            data[$"{sectionName}:connectionString"] = connectionString;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();

        return configuration.GetSection(sectionName);
    }

    [Fact]
    public void DefaultClient_IsRegistered_AndResolvableAsUnkeyedService()
    {
        var services = new ServiceCollection();

        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            null,
            BuildClientConfigurationSection());

        using var provider = services.BuildServiceProvider();

        // A default (unnamed) Azure client is resolvable directly from DI.
        var client = provider.GetRequiredService<TableServiceClient>();
        Assert.NotNull(client);

        // The factory for the client is always registered by AddAzureClients.
        Assert.NotNull(provider.GetRequiredService<IAzureClientFactory<TableServiceClient>>());
    }

    [Fact]
    public void KeyedClient_IsRegistered_AndResolvableAsKeyedService()
    {
        const string key = "primary";
        var services = new ServiceCollection();

        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            key,
            BuildClientConfigurationSection());

        using var provider = services.BuildServiceProvider();

        // The keyed singleton registered by the helper resolves the named client from the factory.
        var keyedClient = provider.GetRequiredKeyedService<TableServiceClient>(key);
        Assert.NotNull(keyedClient);

        // The factory can create the named client directly as well.
        var factory = provider.GetRequiredService<IAzureClientFactory<TableServiceClient>>();
        Assert.NotNull(factory.CreateClient(key));
    }

    [Fact]
    public void KeyedClient_ResolvesSameSingletonInstance()
    {
        const string key = "primary";
        var services = new ServiceCollection();

        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            key,
            BuildClientConfigurationSection());

        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredKeyedService<TableServiceClient>(key);
        var second = provider.GetRequiredKeyedService<TableServiceClient>(key);

        // Registered via AddKeyedSingleton -> same instance across resolutions.
        Assert.Same(first, second);
    }

    [Fact]
    public void WhitespaceServiceKey_IsTreatedAsDefault_NotKeyed()
    {
        var services = new ServiceCollection();

        // Whitespace key is normalized to null by the helper => default (unkeyed) registration.
        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            "   ",
            BuildClientConfigurationSection());

        using var provider = services.BuildServiceProvider();

        // Resolvable as the default client...
        Assert.NotNull(provider.GetRequiredService<TableServiceClient>());

        // ...but NOT as a keyed service under the whitespace key.
        Assert.Null(provider.GetKeyedService<TableServiceClient>("   "));
    }

    [Fact]
    public void ConfigureOptions_Delegate_IsInvoked()
    {
        var services = new ServiceCollection();
        var configureInvoked = false;

        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            null,
            BuildClientConfigurationSection(),
            options =>
            {
                configureInvoked = true;
                // Mutate an option to prove the delegate can influence the built client.
                options.EnableTenantDiscovery = true;
            });

        using var provider = services.BuildServiceProvider();

        // Resolving the client forces the option configuration pipeline to run.
        _ = provider.GetRequiredService<TableServiceClient>();

        Assert.True(configureInvoked);
    }

    [Fact]
    public void ConfigureOptions_Null_DoesNotThrow()
    {
        var services = new ServiceCollection();

        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            null,
            BuildClientConfigurationSection(),
            null);

        using var provider = services.BuildServiceProvider();

        // The helper wraps a null delegate in a null-conditional invoke; resolution must still succeed.
        Assert.NotNull(provider.GetRequiredService<TableServiceClient>());
    }

    [Fact]
    public void ConnectionString_FromConfiguration_IsBound_ToClient()
    {
        var services = new ServiceCollection();

        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            null,
            BuildClientConfigurationSection());

        using var provider = services.BuildServiceProvider();

        // The account name in the fake connection string must surface on the built client,
        // proving the IConfigurationSection was actually bound by the client factory.
        var client = provider.GetRequiredService<TableServiceClient>();
        Assert.Equal("devstoreaccount1", client.AccountName);
    }

    [Fact]
    public void MultipleKeyedClients_AreIndependentlyRegistered()
    {
        var services = new ServiceCollection();

        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            "alpha",
            BuildClientConfigurationSection("alpha"));
        services.IgniteAzureClient<TableServiceClient, TableClientOptions>(
            "beta",
            BuildClientConfigurationSection("beta"));

        using var provider = services.BuildServiceProvider();

        var alpha = provider.GetRequiredKeyedService<TableServiceClient>("alpha");
        var beta = provider.GetRequiredKeyedService<TableServiceClient>("beta");

        Assert.NotNull(alpha);
        Assert.NotNull(beta);
        Assert.NotSame(alpha, beta);
    }
}