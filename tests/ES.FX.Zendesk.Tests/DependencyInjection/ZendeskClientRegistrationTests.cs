using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;

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
}