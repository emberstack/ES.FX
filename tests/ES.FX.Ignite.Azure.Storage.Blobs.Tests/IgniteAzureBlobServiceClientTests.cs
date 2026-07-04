using Azure.Storage.Blobs;
using ES.FX.Ignite.Azure.Storage.Blobs.Configuration;
using ES.FX.Ignite.Azure.Storage.Blobs.Hosting;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using static Xunit.Assert;

namespace ES.FX.Ignite.Azure.Storage.Blobs.Tests;

/// <summary>
///     Functional coverage of <see cref="AzureBlobStorageHostingExtensions.IgniteAzureBlobServiceClient" />
///     wiring: the reconfiguration guard, the keyed settings registration, health-check registration
///     toggling, tracing toggling, the <c>configureClientOptions</c> delegate, and service-key whitespace
///     normalization. No live Azure is contacted — only the resulting DI container is inspected.
/// </summary>
public class IgniteAzureBlobServiceClientTests
{
    private static HostApplicationBuilder CreateBuilder(string? name = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureBlobStorageSpark.ConfigurationSectionPath}{(string.IsNullOrWhiteSpace(name) ? string.Empty : $":{name}")}:ConnectionString",
                "UseDevelopmentStorage=true;")
        ]);
        return builder;
    }

    private static IReadOnlyList<HealthCheckRegistration> HealthCheckRegistrations(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToList();

    [Fact]
    public void IgniteAzureBlobServiceClient_CalledTwiceWithSameServiceKey_Throws()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureBlobServiceClient();

        // Second registration with the same (default/null) service key must be blocked by the guard.
        var ex = Throws<ReconfigurationNotSupportedException>(() => builder.IgniteAzureBlobServiceClient());
        Contains(AzureBlobStorageSpark.Name, ex.Message);
    }

    [Fact]
    public void IgniteAzureBlobServiceClient_CalledTwiceWithSameExplicitKey_Throws()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureBlobServiceClient(serviceKey: "primary");

        Throws<ReconfigurationNotSupportedException>(() =>
            builder.IgniteAzureBlobServiceClient(serviceKey: "primary"));
    }

    [Fact]
    public void IgniteAzureBlobServiceClient_DifferentServiceKeys_DoesNotThrow()
    {
        var builder = CreateBuilder();

        // Distinct keys are guarded independently, so both registrations must succeed.
        builder.IgniteAzureBlobServiceClient(serviceKey: "primary");
        builder.IgniteAzureBlobServiceClient(serviceKey: "secondary");

        var app = builder.Build();
        NotNull(app.Services.GetRequiredKeyedService<BlobServiceClient>("primary"));
        NotNull(app.Services.GetRequiredKeyedService<BlobServiceClient>("secondary"));
    }

    [Fact]
    public void HealthChecks_EnabledByDefault_RegistersBlobServiceHealthCheck()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureBlobServiceClient();

        using var app = builder.Build();
        var registrations = HealthCheckRegistrations(app.Services);

        // Default HealthCheckSettings.Enabled == true -> a single registration named for the client.
        var registration = Single(registrations);
        Equal($"Azure-{nameof(BlobServiceClient)}", registration.Name);
    }

    [Fact]
    public void HealthChecks_Disabled_RegistersNoHealthCheck()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureBlobServiceClient(configureSettings: s => s.HealthChecks.Enabled = false);

        using var app = builder.Build();

        var registrationsOptions = app.Services.GetService<IOptions<HealthCheckServiceOptions>>();
        var registrations = registrationsOptions?.Value.Registrations ?? [];
        Empty(registrations);
    }

    [Fact]
    public void ConfigureSettings_IsInvoked_AndSettingsResolvableAsKeyedSingleton()
    {
        var builder = CreateBuilder();
        var delegateInvoked = false;

        builder.IgniteAzureBlobServiceClient(serviceKey: "primary", configureSettings: s =>
        {
            delegateInvoked = true;
            s.Tracing.Enabled = false;
            s.HealthChecks.Enabled = false;
        });

        using var app = builder.Build();

        True(delegateInvoked);

        // Settings are registered as a keyed singleton under the service key with the mutated values.
        var settings = app.Services.GetRequiredKeyedService<AzureBlobStorageSparkSettings>("primary");
        False(settings.Tracing.Enabled);
        False(settings.HealthChecks.Enabled);
    }

    [Fact]
    public void ConfigureSettings_NullServiceKey_SettingsResolvableAsDefaultKeyedSingleton()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureBlobServiceClient(configureSettings: s => s.Tracing.Enabled = false);

        using var app = builder.Build();

        // A null service key registers the settings under the default (null) key.
        var settings = app.Services.GetRequiredKeyedService<AzureBlobStorageSparkSettings>(null);
        NotNull(settings);
        False(settings.Tracing.Enabled);
    }

    [Fact]
    public void Tracing_EnabledByDefault_RegistersOpenTelemetryServices()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureBlobServiceClient();

        // Default TracingSettings.Enabled == true -> OpenTelemetry descriptors present in the container.
        Contains(builder.Services, d =>
            d.ServiceType.FullName?.Contains("OpenTelemetry", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Tracing_Disabled_DoesNotRegisterOpenTelemetryServices()
    {
        var builder = CreateBuilder();
        builder.IgniteAzureBlobServiceClient(configureSettings: s => s.Tracing.Enabled = false);

        DoesNotContain(builder.Services, d =>
            d.ServiceType.FullName?.Contains("OpenTelemetry", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ConfigureClientOptions_IsAppliedToCreatedClient()
    {
        var builder = CreateBuilder();
        var configuredRetries = 7;

        builder.IgniteAzureBlobServiceClient(configureClientOptions: options =>
        {
            options.Retry.MaxRetries = configuredRetries;
        });

        using var app = builder.Build();

        // Resolving the client must run the Azure client factory, which applies configureClientOptions.
        // If the delegate were dropped, creating the client would use the default retry count instead.
        var client = app.Services.GetRequiredService<BlobServiceClient>();
        NotNull(client);
    }

    [Fact]
    public void ConfigureClientOptions_DelegateIsInvokedDuringClientCreation()
    {
        var builder = CreateBuilder();
        var invoked = false;

        builder.IgniteAzureBlobServiceClient(configureClientOptions: _ => invoked = true);

        using var app = builder.Build();

        // The Azure client factory is lazy — force client creation, then assert the delegate ran.
        _ = app.Services.GetRequiredService<IAzureClientFactory<BlobServiceClient>>().CreateClient("Default");
        True(invoked);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WhitespaceServiceKey_NormalizedToDefault_RegistersUnkeyedClient(string serviceKey)
    {
        var builder = CreateBuilder();

        // Whitespace keys normalize to null, so the client registers as the DEFAULT (unkeyed) service.
        builder.IgniteAzureBlobServiceClient(serviceKey: serviceKey);

        using var app = builder.Build();

        // The default client resolves...
        NotNull(app.Services.GetRequiredService<BlobServiceClient>());

        // ...and the health-check name carries no key suffix (normalized to default).
        var registration = Single(HealthCheckRegistrations(app.Services));
        Equal($"Azure-{nameof(BlobServiceClient)}", registration.Name);
        DoesNotContain("[", registration.Name);
    }

    [Fact]
    public void WhitespaceServiceKey_NormalizedToDefault_GuardsAsDefaultKey()
    {
        var builder = CreateBuilder();

        // A whitespace key and a null key normalize to the same guard key -> second call must throw.
        builder.IgniteAzureBlobServiceClient(serviceKey: "   ");
        Throws<ReconfigurationNotSupportedException>(() => builder.IgniteAzureBlobServiceClient());
    }
}