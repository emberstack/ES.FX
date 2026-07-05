using System.Net;
using ES.FX.Zendesk.Attachments;
using ES.FX.Zendesk.Configuration;
using ES.FX.Zendesk.HelpCenter;
using ES.FX.Zendesk.Support;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;

namespace ES.FX.Zendesk.Tests.DependencyInjection;

public class ZendeskClientRegistrationTests
{
    private static ServiceCollection CreateServices(RoutingHandler routing)
    {
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => routing));
        return services;
    }

    [Fact]
    public async Task Default_Registration_Resolves_All_Services()
    {
        var services = CreateServices(new RoutingHandler());
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme";
            options.OAuth.ClientId = "cid";
            options.OAuth.ClientSecret = "secret";
        });

        await using var provider = services.BuildServiceProvider();

        // A null service key registers the DEFAULT instances — plain GetRequiredService must resolve them all.
        Assert.NotNull(provider.GetRequiredService<ZendeskSupportApiClient>());
        Assert.NotNull(provider.GetRequiredService<ZendeskHelpCenterApiClient>());
        Assert.NotNull(provider.GetRequiredService<ZendeskAttachmentContentFetcher>());
        Assert.NotNull(provider.GetRequiredService<IRequestAdapter>());
    }

    [Fact]
    public async Task Keyed_Registration_Resolves_All_Services()
    {
        var services = CreateServices(new RoutingHandler());
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme-staging";
            options.OAuth.ClientId = "cid";
            options.OAuth.ClientSecret = "secret";
        }, "staging");

        await using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredKeyedService<ZendeskSupportApiClient>("staging"));
        Assert.NotNull(provider.GetRequiredKeyedService<ZendeskHelpCenterApiClient>("staging"));
        Assert.NotNull(provider.GetRequiredKeyedService<ZendeskAttachmentContentFetcher>("staging"));
        Assert.NotNull(provider.GetRequiredKeyedService<IRequestAdapter>("staging"));
    }

    [Fact]
    public async Task Adapter_BaseUrl_Targets_The_Service_Root_Derived_From_The_Subdomain()
    {
        var services = CreateServices(new RoutingHandler());
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme";
            options.OAuth.ClientId = "cid";
            options.OAuth.ClientSecret = "secret";
        });

        await using var provider = services.BuildServiceProvider();

        // The generated request templates carry the full /api/v2/… path, so the adapter must target the host
        // root (NOT the /api/v2/ base address) or every request would hit /api/v2/api/v2/….
        var adapter = provider.GetRequiredService<IRequestAdapter>();
        Assert.Equal(new ZendeskClientOptions { Subdomain = "acme" }.GetServiceRootAddress().ToString().TrimEnd('/'),
            adapter.BaseUrl);
        Assert.Equal("https://acme.zendesk.com", adapter.BaseUrl);
    }

    [Fact]
    public async Task Adapter_BaseUrl_Preserves_A_BaseUrl_Override_Path_Prefix()
    {
        var services = CreateServices(new RoutingHandler());
        services.AddZendeskClient(options =>
        {
            options.BaseUrl = "https://sandbox.example.com/proxy/api/v2/";
            options.OAuth.ClientId = "cid";
            options.OAuth.ClientSecret = "secret";
        }, "sandbox");

        await using var provider = services.BuildServiceProvider();

        var adapter = provider.GetRequiredKeyedService<IRequestAdapter>("sandbox");
        Assert.Equal("https://sandbox.example.com/proxy", adapter.BaseUrl);
    }

    [Fact]
    public async Task Api_Calls_Acquire_A_Token_And_Carry_The_Bearer_Through_The_Auth_Handler()
    {
        var routing = new RoutingHandler();
        var services = CreateServices(routing);
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme";
            options.OAuth.ClientId = "cid";
            options.OAuth.ClientSecret = "secret";
        });

        await using var provider = services.BuildServiceProvider();
        var support = provider.GetRequiredService<ZendeskSupportApiClient>();

        var response = await support.Api.V2.Tickets[1]
            .GetAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, response?.Ticket?.Id); // the canned envelope round-tripped through the generated model
        Assert.True(routing.TokenCalls >= 1); // acquired an OAuth token first
        Assert.Equal("Bearer", routing.LastApiAuthScheme); // the auth handler IS in the chain
        var request = Assert.Single(routing.ApiRequests);
        Assert.Equal("acme.zendesk.com", request.Host);
        Assert.Equal("/api/v2/tickets/1", request.Path);
        Assert.Equal("tok-cid", request.BearerToken); // the bearer minted from THESE credentials
        // Help Center endpoints return HTTP 415 without an explicit JSON Accept header — assert it is sent.
        Assert.Contains("application/json", routing.LastApiAccept!);
    }

    [Fact]
    public async Task Response_Guard_Handler_Is_In_The_Chain_And_Translates_A_NonSuccess_Status()
    {
        var routing = new RoutingHandler
        {
            ApiStatusCode = HttpStatusCode.NotFound,
            ApiResponseJson = """{"error":"RecordNotFound"}"""
        };
        var services = CreateServices(routing);
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme";
            options.OAuth.ClientId = "cid";
            options.OAuth.ClientSecret = "secret";
        });

        await using var provider = services.BuildServiceProvider();
        var support = provider.GetRequiredService<ZendeskSupportApiClient>();

        // The guard handler sits inside the pipeline: a non-retryable status must surface as the rich typed
        // exception (status + body), not as Kiota's body-less ApiException.
        var exception = await Assert.ThrowsAsync<ZendeskApiException>(async () =>
            await support.Api.V2.Tickets[404].GetAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Contains("RecordNotFound", exception.ResponseBody);
        Assert.Equal("Bearer", routing.LastApiAuthScheme); // and the auth handler still ran before it
    }

    [Fact]
    public async Task Sends_Product_User_Agent_On_Api_And_Token_Requests()
    {
        var routing = new RoutingHandler();
        var services = CreateServices(routing);
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "acme";
            options.OAuth.ClientId = "cid";
            options.OAuth.ClientSecret = "secret";
        });

        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<ZendeskSupportApiClient>().Api.V2.Tickets[1]
            .GetAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.StartsWith("ES.FX.Zendesk/", routing.LastApiUserAgent);
        Assert.StartsWith("ES.FX.Zendesk/", routing.LastTokenUserAgent);
    }

    [Fact]
    public async Task Keyed_Instances_Are_Independent_And_Use_Their_Own_Credentials_And_Tokens()
    {
        // The stub token endpoint mints "tok-{client_id}" from the credentials it receives, so the bearer on an
        // API request proves WHICH client_id acquired it — the cross-tenant credential-leak guard.
        var routing = new RoutingHandler();
        var services = CreateServices(routing);
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

        var alpha = provider.GetRequiredKeyedService<ZendeskSupportApiClient>("alpha");
        var beta = provider.GetRequiredKeyedService<ZendeskSupportApiClient>("beta");
        Assert.NotSame(alpha, beta);

        await alpha.Api.V2.Tickets[1].GetAsync(cancellationToken: TestContext.Current.CancellationToken);
        await beta.Api.V2.Tickets[1].GetAsync(cancellationToken: TestContext.Current.CancellationToken);

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
        var services = CreateServices(routing);
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

        await provider.GetRequiredService<ZendeskSupportApiClient>().Api.V2.Tickets[1]
            .GetAsync(cancellationToken: TestContext.Current.CancellationToken);
        await provider.GetRequiredKeyedService<ZendeskSupportApiClient>("staging").Api.V2.Tickets[1]
            .GetAsync(cancellationToken: TestContext.Current.CancellationToken);

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