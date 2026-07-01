using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using ES.FX.Ignite.Zendesk.Configuration;
using ES.FX.Ignite.Zendesk.Hosting;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.Zendesk.Tests;

public class HostingTests
{
    private static HostApplicationBuilder CreateBuilder(params KeyValuePair<string, string?>[] configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(configuration);
        return builder;
    }

    private static KeyValuePair<string, string?> Setting(string key, string? value) =>
        new($"{ZendeskClientSpark.ConfigurationSectionPath}:{key}", value);

    private static KeyValuePair<string, string?>[] ValidCredentials() =>
    [
        Setting("Subdomain", "acme"),
        Setting("OAuth:ClientId", "cid"),
        Setting("OAuth:ClientSecret", "secret")
    ];

    [Fact]
    public void Binds_Options_From_Configuration()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteZendeskClient();

        var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<ZendeskClientOptions>>().Value;
        Assert.Equal("acme", options.Subdomain);
        Assert.Equal("cid", options.OAuth.ClientId);
    }

    [Fact]
    public void Registers_ApiClient_And_Settings()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteZendeskClient();

        var app = builder.Build();

        Assert.NotNull(app.Services.GetRequiredService<IZendeskClient>());
        Assert.NotNull(app.Services.GetRequiredService<ZendeskClientSparkSettings>());
    }

    [Fact]
    public void Registers_HealthCheck_By_Default()
    {
        var builder = CreateBuilder();
        builder.IgniteZendeskClient();

        var app = builder.Build();

        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Contains(registrations, registration => registration.Name == ZendeskClientSpark.Name);
    }

    [Fact]
    public void HealthCheck_Can_Be_Disabled_Via_Settings()
    {
        var builder = CreateBuilder(Setting($"{SparkConfig.Settings}:HealthChecks:Enabled", "false"));
        builder.IgniteZendeskClient();

        var app = builder.Build();

        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.DoesNotContain(registrations, registration => registration.Name == ZendeskClientSpark.Name);
    }

    [Fact]
    public void Does_Not_Allow_Reconfiguration_Of_Same_Instance()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteZendeskClient();

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteZendeskClient());
    }

    [Fact]
    public void Supports_Multiple_Named_Keyed_Instances()
    {
        var builder = CreateBuilder(
            Setting("Subdomain", "acme"),
            Setting("OAuth:ClientId", "cid"),
            Setting("OAuth:ClientSecret", "secret"),
            Setting("sandbox:Subdomain", "acme-sandbox"),
            Setting("sandbox:OAuth:ClientId", "cid-sandbox"),
            Setting("sandbox:OAuth:ClientSecret", "secret-sandbox"));
        builder.IgniteZendeskClient();
        builder.IgniteZendeskClient("sandbox", "sandbox");

        var app = builder.Build();

        Assert.NotNull(app.Services.GetRequiredService<IZendeskClient>());
        Assert.NotNull(app.Services.GetRequiredKeyedService<IZendeskClient>("sandbox"));

        var monitor = app.Services.GetRequiredService<IOptionsMonitor<ZendeskClientOptions>>();
        Assert.Equal("acme", monitor.Get(string.Empty).Subdomain);
        Assert.Equal("acme-sandbox", monitor.Get("sandbox").Subdomain);

        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Contains(registrations, r => r.Name == ZendeskClientSpark.Name);
        Assert.Contains(registrations, r => r.Name == $"{ZendeskClientSpark.Name}[sandbox]");
    }

    [Fact]
    public void Different_ServiceKeys_Do_Not_Conflict()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteZendeskClient(serviceKey: "a");
        builder.IgniteZendeskClient(serviceKey: "b");

        var app = builder.Build();

        Assert.NotNull(app.Services.GetRequiredKeyedService<IZendeskClient>("a"));
        Assert.NotNull(app.Services.GetRequiredKeyedService<IZendeskClient>("b"));
    }
}