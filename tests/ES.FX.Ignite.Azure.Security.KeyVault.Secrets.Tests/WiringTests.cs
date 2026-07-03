using ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Configuration;
using ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Hosting;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using static Xunit.Assert;

namespace ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Tests;

/// <summary>
///     Verifies the observability wiring, duplicate-registration guard, and configureSettings behavior of
///     <see cref="AzureKeyVaultSecretsHostingExtensions.IgniteAzureKeyVaultSecretClient" />.
/// </summary>
public class WiringTests
{
    private static HostApplicationBuilder CreateBuilder()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureKeyVaultSecretsSpark.ConfigurationSectionPath}:VaultUri", "https://vaulturi")
        ]);
        return builder;
    }

    [Fact]
    public void IgniteAzureKeyVaultSecretClient_RegistersHealthCheck_WhenEnabled()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureKeyVaultSecretClient();

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        var registration = Single(options.Registrations, r => r.Name == "Azure-SecretClient");
        // Default tags include the component group and client type name.
        Contains("Azure", registration.Tags);
        Contains("SecretClient", registration.Tags);
    }

    [Fact]
    public void IgniteAzureKeyVaultSecretClient_RegistersKeyedHealthCheckName_WhenServiceKeyProvided()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureKeyVaultSecretClient(serviceKey: "keyed");

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        Contains(options.Registrations, r => r.Name == "Azure-SecretClient-[keyed]");
    }

    [Fact]
    public void IgniteAzureKeyVaultSecretClient_DoesNotRegisterHealthCheck_WhenDisabled()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureKeyVaultSecretClient(
            configureSettings: settings => settings.HealthChecks.Enabled = false);

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        DoesNotContain(options.Registrations, r => r.Name == "Azure-SecretClient");
    }

    [Fact]
    public void IgniteAzureKeyVaultSecretClient_ThrowsOnDuplicateRegistration_ForSameServiceKey()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureKeyVaultSecretClient(serviceKey: "keyed");

        // Second call with the same service key must be blocked by the spark guard.
        Throws<ReconfigurationNotSupportedException>(() =>
            builder.IgniteAzureKeyVaultSecretClient(serviceKey: "keyed"));
    }

    [Fact]
    public void IgniteAzureKeyVaultSecretClient_AllowsDistinctServiceKeys()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureKeyVaultSecretClient(serviceKey: "a");

        // Different service key produces a different guard key — must not throw.
        var exception = Record.Exception(() => builder.IgniteAzureKeyVaultSecretClient(serviceKey: "b"));
        Null(exception);
    }

    [Fact]
    public void ConfigureSettings_Delegate_IsInvokedAndAppliedToRegisteredSettings()
    {
        var builder = CreateBuilder();
        var invoked = false;

        builder.IgniteAzureKeyVaultSecretClient(configureSettings: settings =>
        {
            invoked = true;
            settings.Tracing.Enabled = false;
            settings.HealthChecks.Tags = ["custom-tag"];
        });

        True(invoked, "configureSettings delegate should be invoked.");

        var app = builder.Build();

        // The mutated settings are registered as a keyed singleton (default key => null key).
        var settings = app.Services.GetRequiredService<AzureKeyVaultSecretsSparkSettings>();
        False(settings.Tracing.Enabled);
        Contains("custom-tag", settings.HealthChecks.Tags);

        // And the mutation flowed into the actual health check registration tags.
        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var registration = Single(options.Registrations, r => r.Name == "Azure-SecretClient");
        Contains("custom-tag", registration.Tags);
    }

    [Fact]
    public void ConfigureSettings_RunsAfterConfigurationBinding()
    {
        // Bind a value via configuration, then have configureSettings override it — the delegate must win,
        // proving it runs after binding.
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureKeyVaultSecretsSpark.ConfigurationSectionPath}:VaultUri", "https://vaulturi"),
            new KeyValuePair<string, string?>(
                $"{AzureKeyVaultSecretsSpark.ConfigurationSectionPath}:HealthChecks:Enabled", "true")
        ]);

        builder.IgniteAzureKeyVaultSecretClient(
            configureSettings: settings => settings.HealthChecks.Enabled = false);

        var app = builder.Build();
        var settings = app.Services.GetRequiredService<AzureKeyVaultSecretsSparkSettings>();

        // Config bound Enabled=true, delegate set it false afterward -> false wins.
        False(settings.HealthChecks.Enabled);
    }
}
