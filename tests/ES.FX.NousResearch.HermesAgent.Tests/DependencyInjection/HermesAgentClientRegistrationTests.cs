using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Configuration;
using ES.FX.NousResearch.HermesAgent.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ES.FX.NousResearch.HermesAgent.Tests.DependencyInjection;

public class HermesAgentClientRegistrationTests
{
    [Fact]
    public async Task Registers_Default_Client_With_Bearer_Accept_And_User_Agent()
    {
        var routing = new RoutingHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        services.AddHermesAgentClient(configureOptions: options =>
        {
            options.BaseUrl = "http://localhost:8642";
            options.ApiKey = "default-key";
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHermesAgentClient>();

        var models = await client.Server.GetModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("hermes-agent", Assert.Single(models.Data).Id);
        Assert.Equal("Bearer", routing.LastAuthorizationScheme);
        Assert.Equal("default-key", routing.LastAuthorizationParameter);
        Assert.Contains("application/json", routing.LastAccept!);
        Assert.StartsWith("ES.FX.NousResearch.HermesAgent/", routing.LastUserAgent);
    }

    [Fact]
    public async Task BaseUrl_Without_Trailing_Slash_Composes_Relative_Request_Paths()
    {
        var routing = new RoutingHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        services.AddHermesAgentClient(configureOptions: options =>
        {
            options.BaseUrl = "http://localhost:8642"; // no trailing slash on purpose
            options.ApiKey = "key";
        });

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IHermesAgentClient>()
            .Server.GetModelsAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(routing.Requests);
        Assert.Equal("http://localhost:8642/v1/models", request.AbsoluteUri);
    }

    [Fact]
    public async Task BaseUrl_With_A_Path_Prefix_Composes_Area_Requests_Under_The_Prefix()
    {
        // A reverse-proxied server under a path prefix (e.g. /hermes/) must keep the prefix on EVERY area's
        // requests — this holds only while area request URIs stay relative (a leading slash would silently
        // drop the prefix and 404 in such deployments, while composing identically against a root base).
        var routing = new RoutingHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        services.AddHermesAgentClient(configureOptions: options =>
        {
            options.BaseUrl = "https://gateway.example.com/hermes";
            options.ApiKey = "key";
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHermesAgentClient>();

        await client.Server.GetModelsAsync(TestContext.Current.CancellationToken);
        await client.Sessions.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(routing.Requests, r => r.AbsoluteUri == "https://gateway.example.com/hermes/v1/models");
        Assert.Contains(routing.Requests, r => r.AbsoluteUri == "https://gateway.example.com/hermes/api/sessions");
    }

    [Fact]
    public async Task Supports_Multiple_Keyed_Instances_With_Isolated_Options()
    {
        var routing = new RoutingHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        services.AddHermesAgentClient("a", options =>
        {
            options.BaseUrl = "http://hermes-a.local:8642";
            options.ApiKey = "key-a";
        });
        services.AddHermesAgentClient("b", options =>
        {
            options.BaseUrl = "http://hermes-b.local:8642";
            options.ApiKey = "key-b";
        });

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredKeyedService<IHermesAgentClient>("a")
            .Server.GetHealthAsync(TestContext.Current.CancellationToken);
        await provider.GetRequiredKeyedService<IHermesAgentClient>("b")
            .Server.GetHealthAsync(TestContext.Current.CancellationToken);

        // Each keyed instance must hit ITS OWN host with ITS OWN key — no cross-contamination.
        Assert.Contains(routing.Requests, r => r is { Host: "hermes-a.local", BearerKey: "key-a" });
        Assert.Contains(routing.Requests, r => r is { Host: "hermes-b.local", BearerKey: "key-b" });
        Assert.DoesNotContain(routing.Requests, r => r.Host == "hermes-a.local" && r.BearerKey == "key-b");
        Assert.DoesNotContain(routing.Requests, r => r.Host == "hermes-b.local" && r.BearerKey == "key-a");
    }

    [Fact]
    public async Task Default_And_Keyed_Instances_Coexist_With_Isolated_Options()
    {
        var routing = new RoutingHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        services.AddHermesAgentClient(configureOptions: options =>
        {
            options.BaseUrl = "http://hermes-default.local:8642";
            options.ApiKey = "key-default";
        });
        services.AddHermesAgentClient("staging", options =>
        {
            options.BaseUrl = "http://hermes-staging.local:8642";
            options.ApiKey = "key-staging";
        });

        await using var provider = services.BuildServiceProvider();

        // A null service key registers the DEFAULT instance — plain GetRequiredService must resolve it.
        await provider.GetRequiredService<IHermesAgentClient>()
            .Server.GetHealthAsync(TestContext.Current.CancellationToken);
        await provider.GetRequiredKeyedService<IHermesAgentClient>("staging")
            .Server.GetHealthAsync(TestContext.Current.CancellationToken);

        Assert.Contains(routing.Requests, r => r is { Host: "hermes-default.local", BearerKey: "key-default" });
        Assert.Contains(routing.Requests, r => r is { Host: "hermes-staging.local", BearerKey: "key-staging" });

        // Named options are keyed by the service key (default name for the unkeyed instance).
        var monitor = provider.GetRequiredService<IOptionsMonitor<HermesAgentClientOptions>>();
        Assert.Equal("key-default", monitor.Get(Options.DefaultName).ApiKey);
        Assert.Equal("key-staging", monitor.Get("staging").ApiKey);
    }

    [Fact]
    public async Task Resolved_Client_Exposes_All_Six_Areas()
    {
        var services = new ServiceCollection();
        services.AddHermesAgentClient(configureOptions: options =>
        {
            options.BaseUrl = "http://localhost:8642";
            options.ApiKey = "key";
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHermesAgentClient>();

        Assert.NotNull(client.Chat);
        Assert.NotNull(client.Responses);
        Assert.NotNull(client.Runs);
        Assert.NotNull(client.Jobs);
        Assert.NotNull(client.Sessions);
        Assert.NotNull(client.Server);
    }

    [Fact]
    public async Task Options_Validator_Is_Registered_And_Rejects_Misconfiguration()
    {
        var services = new ServiceCollection();
        services.AddHermesAgentClient(); // options never configured → validator must reject

        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptionsMonitor<HermesAgentClientOptions>>().Get(Options.DefaultName));

        Assert.Contains(exception.Failures, f => f.Contains("BaseUrl"));
        Assert.Contains(exception.Failures, f => f.Contains("ApiKey"));
    }
}
