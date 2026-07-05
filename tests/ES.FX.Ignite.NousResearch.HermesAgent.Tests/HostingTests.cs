using ES.FX.Ignite.NousResearch.HermesAgent.Configuration;
using ES.FX.Ignite.NousResearch.HermesAgent.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace ES.FX.Ignite.NousResearch.HermesAgent.Tests;

public class HostingTests
{
    private static HostApplicationBuilder CreateBuilder(params KeyValuePair<string, string?>[] configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(configuration);
        return builder;
    }

    private static KeyValuePair<string, string?> Setting(string key, string? value) =>
        new($"{HermesAgentClientSpark.ConfigurationSectionPath}:{key}", value);

    private static KeyValuePair<string, string?>[] ValidCredentials() =>
    [
        Setting("BaseUrl", "http://localhost:8642"),
        Setting("ApiKey", "test-key")
    ];

    [Fact]
    public void Binds_Options_From_Configuration()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteHermesAgentClient();

        var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<HermesAgentClientOptions>>().Value;
        Assert.Equal("http://localhost:8642", options.BaseUrl);
        Assert.Equal("test-key", options.ApiKey);
    }

    [Fact]
    public void Registers_ApiClient_And_Settings()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteHermesAgentClient();

        var app = builder.Build();

        Assert.NotNull(app.Services.GetRequiredService<IHermesAgentClient>());
        Assert.NotNull(app.Services.GetRequiredService<HermesAgentClientSparkSettings>());
    }

    [Fact]
    public void Registers_HealthCheck_By_Default()
    {
        var builder = CreateBuilder();
        builder.IgniteHermesAgentClient();

        var app = builder.Build();

        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Contains(registrations, registration => registration.Name == HermesAgentClientSpark.Name);
    }

    [Fact]
    public void HealthCheck_Can_Be_Disabled_Via_Settings()
    {
        var builder = CreateBuilder(Setting($"{SparkConfig.Settings}:HealthChecks:Enabled", "false"));
        builder.IgniteHermesAgentClient();

        var app = builder.Build();

        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.DoesNotContain(registrations, registration => registration.Name == HermesAgentClientSpark.Name);
    }

    [Fact]
    public void Binds_Settings_From_Configuration()
    {
        var builder = CreateBuilder(
            Setting($"{SparkConfig.Settings}:HealthChecks:Enabled", "false"),
            Setting($"{SparkConfig.Settings}:Tracing:Enabled", "false"));
        builder.IgniteHermesAgentClient();

        var app = builder.Build();

        var settings = app.Services.GetRequiredService<HermesAgentClientSparkSettings>();
        Assert.False(settings.HealthChecks.Enabled);
        Assert.False(settings.Tracing.Enabled);
    }

    [Fact]
    public void ConfigureSettings_Delegate_Overrides_Configuration()
    {
        var builder = CreateBuilder(Setting($"{SparkConfig.Settings}:HealthChecks:Enabled", "false"));
        builder.IgniteHermesAgentClient(configureSettings: settings => settings.HealthChecks.Enabled = true);

        var app = builder.Build();

        var settings = app.Services.GetRequiredService<HermesAgentClientSparkSettings>();
        Assert.True(settings.HealthChecks.Enabled);

        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Contains(registrations, registration => registration.Name == HermesAgentClientSpark.Name);
    }

    [Fact]
    public void ConfigureOptions_Delegate_Overrides_Configuration()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteHermesAgentClient(configureOptions: options => options.BaseUrl = "http://overridden:9000");

        var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<HermesAgentClientOptions>>().Value;
        Assert.Equal("http://overridden:9000", options.BaseUrl);
        Assert.Equal("test-key", options.ApiKey);
    }

    [Fact]
    public void Does_Not_Allow_Reconfiguration_Of_Same_Instance()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteHermesAgentClient();

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteHermesAgentClient());
    }

    [Fact]
    public void Does_Not_Allow_Reconfiguration_Of_Same_ServiceKey()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteHermesAgentClient(serviceKey: "a");

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteHermesAgentClient(serviceKey: "a"));
    }

    [Fact]
    public void Supports_Multiple_Named_Keyed_Instances()
    {
        var builder = CreateBuilder(
            Setting("BaseUrl", "http://hermes-default:8642"),
            Setting("ApiKey", "default-key"),
            Setting("staging:BaseUrl", "http://hermes-staging:8642"),
            Setting("staging:ApiKey", "staging-key"));
        builder.IgniteHermesAgentClient();
        builder.IgniteHermesAgentClient("staging", "staging");

        var app = builder.Build();

        Assert.NotNull(app.Services.GetRequiredService<IHermesAgentClient>());
        Assert.NotNull(app.Services.GetRequiredKeyedService<IHermesAgentClient>("staging"));
        Assert.NotNull(app.Services.GetRequiredKeyedService<HermesAgentClientSparkSettings>("staging"));

        var monitor = app.Services.GetRequiredService<IOptionsMonitor<HermesAgentClientOptions>>();
        Assert.Equal("http://hermes-default:8642", monitor.Get(string.Empty).BaseUrl);
        Assert.Equal("http://hermes-staging:8642", monitor.Get("staging").BaseUrl);

        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Contains(registrations, r => r.Name == HermesAgentClientSpark.Name);
        Assert.Contains(registrations, r => r.Name == $"{HermesAgentClientSpark.Name}[staging]");
    }

    [Fact]
    public void Different_ServiceKeys_Do_Not_Conflict()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteHermesAgentClient(serviceKey: "a");
        builder.IgniteHermesAgentClient(serviceKey: "b");

        var app = builder.Build();

        Assert.NotNull(app.Services.GetRequiredKeyedService<IHermesAgentClient>("a"));
        Assert.NotNull(app.Services.GetRequiredKeyedService<IHermesAgentClient>("b"));
    }

    [Fact]
    public void Tracing_Enabled_Registers_TracerProvider()
    {
        var builder = CreateBuilder(ValidCredentials());
        builder.IgniteHermesAgentClient();

        var app = builder.Build();

        // When tracing is enabled the Spark calls AddOpenTelemetry().WithTracing(...), which registers a
        // TracerProvider in the container. Resolving it confirms the tracing branch ran.
        Assert.NotNull(app.Services.GetService<TracerProvider>());
    }

    [Fact]
    public void Tracing_Can_Be_Disabled_Via_Settings()
    {
        var builder = CreateBuilder(Setting($"{SparkConfig.Settings}:Tracing:Enabled", "false"));
        builder.IgniteHermesAgentClient();

        var app = builder.Build();

        // With tracing disabled the Spark never calls AddOpenTelemetry().WithTracing(...), so no
        // TracerProvider is registered.
        Assert.Null(app.Services.GetService<TracerProvider>());
    }
}
