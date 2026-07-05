using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Configuration;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ES.FX.Zendesk.Tests.DependencyInjection;

public class ZendeskClientRegistrationTests
{
    [Fact]
    public async Task Registers_Client_Acquires_Token_And_Calls_Api_With_Bearer()
    {
        var routing = new RoutingHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme";
            options.OAuth.ClientId = "cid";
            options.OAuth.ClientSecret = "secret";
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IZendeskClient>();

        var user = await client.Users.GetCurrentUserAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, user.Id);
        Assert.True(routing.TokenCalls >= 1); // acquired an OAuth token
        Assert.Equal("Bearer", routing.LastApiAuthScheme); // API call carried the bearer token
        Assert.Contains("acme.zendesk.com", routing.ApiHosts);
        // Help Center endpoints return HTTP 415 without an explicit JSON Accept header — assert it is sent.
        Assert.Contains("application/json", routing.LastApiAccept!);
    }

    [Fact]
    public async Task Supports_Multiple_Keyed_Instances_With_Isolated_Configuration()
    {
        var routing = new RoutingHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme-a";
            options.OAuth.ClientId = "a";
            options.OAuth.ClientSecret = "s";
        }, "a");
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme-b";
            options.OAuth.ClientId = "b";
            options.OAuth.ClientSecret = "s";
        }, "b");

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredKeyedService<IZendeskClient>("a")
            .Users.GetCurrentUserAsync(TestContext.Current.CancellationToken);
        await provider.GetRequiredKeyedService<IZendeskClient>("b")
            .Users.GetCurrentUserAsync(TestContext.Current.CancellationToken);

        Assert.Contains("acme-a.zendesk.com", routing.ApiHosts);
        Assert.Contains("acme-b.zendesk.com", routing.ApiHosts);
    }

    [Fact]
    public async Task Sends_Product_User_Agent_On_Api_And_Token_Requests()
    {
        var routing = new RoutingHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme";
            options.OAuth.ClientId = "cid";
            options.OAuth.ClientSecret = "secret";
        });

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IZendeskClient>()
            .Users.GetCurrentUserAsync(TestContext.Current.CancellationToken);

        Assert.StartsWith("ES.FX.Zendesk/", routing.LastApiUserAgent);
        Assert.StartsWith("ES.FX.Zendesk/", routing.LastTokenUserAgent);
    }

    [Fact]
    public async Task Keyed_Instances_Use_Their_Own_Credentials_And_Tokens()
    {
        // The stub token endpoint mints "tok-{client_id}" from the credentials it receives, so the bearer on an
        // API request proves WHICH client_id acquired it — the cross-tenant credential-leak guard.
        var routing = new RoutingHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme-alpha";
            options.OAuth.ClientId = "alpha";
            options.OAuth.ClientSecret = "secret-alpha";
        }, "alpha");
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme-beta";
            options.OAuth.ClientId = "beta";
            options.OAuth.ClientSecret = "secret-beta";
        }, "beta");

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredKeyedService<IZendeskClient>("alpha")
            .Users.GetCurrentUserAsync(TestContext.Current.CancellationToken);
        await provider.GetRequiredKeyedService<IZendeskClient>("beta")
            .Users.GetCurrentUserAsync(TestContext.Current.CancellationToken);

        // Each instance's API request must go to ITS host with the bearer minted from ITS OWN client_id.
        Assert.Contains(routing.ApiRequests,
            r => r is { Host: "acme-alpha.zendesk.com", BearerToken: "tok-alpha" });
        Assert.Contains(routing.ApiRequests,
            r => r is { Host: "acme-beta.zendesk.com", BearerToken: "tok-beta" });

        // The cross-tenant token must NEVER appear on the other tenant's requests.
        Assert.DoesNotContain(routing.ApiRequests,
            r => r.Host == "acme-alpha.zendesk.com" && r.BearerToken == "tok-beta");
        Assert.DoesNotContain(routing.ApiRequests,
            r => r.Host == "acme-beta.zendesk.com" && r.BearerToken == "tok-alpha");
    }

    [Fact]
    public async Task Default_And_Keyed_Instances_Coexist_With_Isolated_Options()
    {
        var routing = new RoutingHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme-default";
            options.OAuth.ClientId = "cid-default";
            options.OAuth.ClientSecret = "secret";
        });
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme-staging";
            options.OAuth.ClientId = "cid-staging";
            options.OAuth.ClientSecret = "secret";
        }, "staging");

        await using var provider = services.BuildServiceProvider();

        // A null service key registers the DEFAULT instance — plain GetRequiredService must resolve it.
        await provider.GetRequiredService<IZendeskClient>()
            .Users.GetCurrentUserAsync(TestContext.Current.CancellationToken);
        await provider.GetRequiredKeyedService<IZendeskClient>("staging")
            .Users.GetCurrentUserAsync(TestContext.Current.CancellationToken);

        Assert.Contains(routing.ApiRequests,
            r => r is { Host: "acme-default.zendesk.com", BearerToken: "tok-cid-default" });
        Assert.Contains(routing.ApiRequests,
            r => r is { Host: "acme-staging.zendesk.com", BearerToken: "tok-cid-staging" });

        // Named options are keyed by the service key (default name for the unkeyed instance).
        var monitor = provider.GetRequiredService<IOptionsMonitor<ZendeskClientOptions>>();
        Assert.Equal("acme-default", monitor.Get(Options.DefaultName).Subdomain);
        Assert.Equal("acme-staging", monitor.Get("staging").Subdomain);
    }

    [Fact]
    public async Task Options_Validator_Is_Registered_And_Rejects_Misconfiguration()
    {
        var services = new ServiceCollection();
        services.AddZendeskClient(); // options never configured → empty Subdomain, no OAuth credentials

        await using var provider = services.BuildServiceProvider();

        // Get(...) runs the registered IValidateOptions — this proves the TryAddEnumerable validator wiring,
        // not just the validator class in isolation.
        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptionsMonitor<ZendeskClientOptions>>().Get(Options.DefaultName));

        Assert.Contains(exception.Failures, f => f.Contains("Subdomain"));
        Assert.Contains(exception.Failures, f => f.Contains("ClientId"));
        Assert.Contains(exception.Failures, f => f.Contains("ClientSecret"));
    }
}