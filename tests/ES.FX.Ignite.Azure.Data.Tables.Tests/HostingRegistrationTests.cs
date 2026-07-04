using Azure.Data.Tables;
using ES.FX.Ignite.Azure.Data.Tables.Configuration;
using ES.FX.Ignite.Azure.Data.Tables.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.Azure.Data.Tables.Tests;

/// <summary>
///     Covers configuration binding of <see cref="AzureDataTablesSparkSettings" />, the observability
///     wiring (health-check + tracing registration and their toggles), the duplicate-registration
///     guard, and the programmatic <c>configureSettings</c> override.
/// </summary>
public class HostingRegistrationTests
{
    private static HostApplicationBuilder CreateBuilder(params KeyValuePair<string, string?>[] config)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureDataTablesSpark.ConfigurationSectionPath}:ConnectionString",
                "UseDevelopmentStorage=true;"),
            .. config
        ]);
        return builder;
    }

    private static string SettingsKey(string tail) =>
        $"{AzureDataTablesSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:{tail}";

    [Fact]
    public void Settings_AreResolvableFromDi_WithDefaults()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureTableServiceClient();

        var settings = builder.Build().Services.GetRequiredService<AzureDataTablesSparkSettings>();

        Assert.NotNull(settings);
        // Defaults per HealthCheckSettings/TracingSettings.
        Assert.True(settings.HealthChecks.Enabled);
        Assert.True(settings.Tracing.Enabled);
    }

    [Fact]
    public void Settings_BindFromConfiguration()
    {
        var builder = CreateBuilder(
            new KeyValuePair<string, string?>(
                SettingsKey(
                    $"{nameof(AzureDataTablesSparkSettings.HealthChecks)}:{nameof(HealthCheckSettings.Enabled)}"),
                "false"),
            new KeyValuePair<string, string?>(
                SettingsKey($"{nameof(AzureDataTablesSparkSettings.Tracing)}:{nameof(HealthCheckSettings.Enabled)}"),
                "false"));

        builder.IgniteAzureTableServiceClient();

        var settings = builder.Build().Services.GetRequiredService<AzureDataTablesSparkSettings>();
        Assert.False(settings.HealthChecks.Enabled);
        Assert.False(settings.Tracing.Enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HealthCheck_RegistrationHonorsConfiguration(bool enabled)
    {
        var builder = CreateBuilder(
            new KeyValuePair<string, string?>(
                SettingsKey(
                    $"{nameof(AzureDataTablesSparkSettings.HealthChecks)}:{nameof(HealthCheckSettings.Enabled)}"),
                enabled.ToString()));

        builder.IgniteAzureTableServiceClient();

        var provider = builder.Build().Services;
        // HealthCheckService is only added when at least one health check is registered.
        Assert.Equal(enabled, provider.GetService<HealthCheckService>() is not null);

        if (enabled)
        {
            var registrations = provider.GetRequiredService<
                IOptions<HealthCheckServiceOptions>>().Value.Registrations;
            var registration = Assert.Single(registrations);
            Assert.Equal("Azure-TableServiceClient", registration.Name);
            Assert.Contains("Azure", registration.Tags);
            Assert.Contains(nameof(TableServiceClient), registration.Tags);
        }
    }

    [Fact]
    public void HealthCheck_RegistrationNameCarriesServiceKey()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureTableServiceClient(serviceKey: "primary");

        var registrations = builder.Build().Services.GetRequiredService<
            IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        var registration = Assert.Single(registrations);
        Assert.Equal("Azure-TableServiceClient-[primary]", registration.Name);
    }

    [Fact]
    public void GuardConfigurationKey_ThrowsOnDuplicateServiceKey()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureTableServiceClient();

        Assert.Throws<ReconfigurationNotSupportedException>(() =>
            builder.IgniteAzureTableServiceClient());
    }

    [Fact]
    public void GuardConfigurationKey_AllowsDistinctServiceKeys()
    {
        var builder = CreateBuilder();

        builder.IgniteAzureTableServiceClient(serviceKey: "a");
        // Distinct service key must NOT trip the guard.
        var exception = Record.Exception(() => builder.IgniteAzureTableServiceClient(serviceKey: "b"));

        Assert.Null(exception);

        var app = builder.Build();
        Assert.NotNull(app.Services.GetKeyedService<TableServiceClient>("a"));
        Assert.NotNull(app.Services.GetKeyedService<TableServiceClient>("b"));

        // Two distinct health-check registrations, one per key.
        var registrations = app.Services.GetRequiredService<
            IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Equal(2, registrations.Count);
    }

    [Fact]
    public void ConfigureSettings_OverrideTakesEffect_AndSuppressesHealthCheck()
    {
        var builder = CreateBuilder(
            // Config enables health checks; the delegate should override it to disabled.
            new KeyValuePair<string, string?>(
                SettingsKey(
                    $"{nameof(AzureDataTablesSparkSettings.HealthChecks)}:{nameof(HealthCheckSettings.Enabled)}"),
                "true"));

        var delegateObservedEnabled = false;
        builder.IgniteAzureTableServiceClient(configureSettings: settings =>
        {
            // The delegate runs after configuration is bound: it must see the bound value.
            delegateObservedEnabled = settings.HealthChecks.Enabled;
            settings.HealthChecks.Enabled = false;
        });

        var provider = builder.Build().Services;

        Assert.True(delegateObservedEnabled);
        var settings = provider.GetRequiredService<AzureDataTablesSparkSettings>();
        Assert.False(settings.HealthChecks.Enabled);
        // Override wins: no health check registered.
        Assert.Null(provider.GetService<HealthCheckService>());
    }
}