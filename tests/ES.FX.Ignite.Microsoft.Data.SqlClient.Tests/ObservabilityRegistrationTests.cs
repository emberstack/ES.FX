using ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Tests;

/// <summary>
///     Covers the observability wiring in <see cref="SqlServerClientHostingExtensions" />: the
///     health-check registration name/tags/failure-status/timeout, the keyed suffix, the
///     enabled/disabled guard, and the health-check factory resolving the correct (keyed vs default)
///     options. These assert the real registration effects, not just the settings POCO.
/// </summary>
public class ObservabilityRegistrationTests
{
    private const string DummyConnectionString = "Server=(local);Database=x;";

    private static ICollection<HealthCheckRegistration> GetRegistrations(IHost app) =>
        app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

    [Fact]
    public void HealthCheck_Registration_Default_Name_Tags_FailureStatus_Timeout()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database",
            configureSettings: settings =>
            {
                settings.HealthChecks.Enabled = true;
                settings.HealthChecks.Tags = ["custom-tag"];
                settings.HealthChecks.FailureStatus = HealthStatus.Degraded;
                settings.HealthChecks.Timeout = TimeSpan.FromSeconds(7);
            },
            configureOptions: o => o.ConnectionString = DummyConnectionString);

        var app = builder.Build();
        var registration = Assert.Single(GetRegistrations(app));

        // Name must be exactly the spark name (no keyed suffix for a default client).
        Assert.Equal(SqlServerClientSpark.Name, registration.Name);
        Assert.Equal("SqlServerClient", registration.Name);

        // Tags must be prefixed with the spark name and include the configured custom tags.
        Assert.Contains(SqlServerClientSpark.Name, registration.Tags);
        Assert.Contains("custom-tag", registration.Tags);

        // FailureStatus and Timeout must flow through from settings to the registration.
        Assert.Equal(HealthStatus.Degraded, registration.FailureStatus);
        Assert.Equal(TimeSpan.FromSeconds(7), registration.Timeout);
    }

    [Fact]
    public void HealthCheck_Registration_Keyed_Name_Is_Suffixed()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database", serviceKey: "primary",
            configureSettings: s => s.HealthChecks.Enabled = true,
            configureOptions: o => o.ConnectionString = DummyConnectionString);

        var app = builder.Build();
        var registration = Assert.Single(GetRegistrations(app));

        // A keyed client must suffix the registration name with the service key.
        Assert.Equal("SqlServerClient[primary]", registration.Name);
    }

    [Fact]
    public void HealthChecks_Not_Registered_When_Disabled()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database",
            configureSettings: s => s.HealthChecks.Enabled = false,
            configureOptions: o => o.ConnectionString = DummyConnectionString);

        var app = builder.Build();

        // With health checks disabled no registration must be added (guards the if branch).
        Assert.Empty(GetRegistrations(app));
    }

    [Fact]
    public void HealthCheck_Registered_When_Enabled()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database",
            configureSettings: s => s.HealthChecks.Enabled = true,
            configureOptions: o => o.ConnectionString = DummyConnectionString);

        var app = builder.Build();

        // With health checks enabled exactly one registration must exist.
        Assert.Single(GetRegistrations(app));
    }

    [Fact]
    public void HealthCheck_Factory_Builds_SimpleSqlServerHealthCheck_From_Default_Options()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database",
            configureSettings: s => s.HealthChecks.Enabled = true,
            configureOptions: o => o.ConnectionString = DummyConnectionString);

        var app = builder.Build();
        var registration = Assert.Single(GetRegistrations(app));

        // Invoking the factory proves it resolves the (default) options and builds the shipped
        // SimpleSqlServerHealthCheck. A wrong keyed lookup or a broken guard would throw here.
        var healthCheck = registration.Factory(app.Services);
        Assert.Equal("SimpleSqlServerHealthCheck", healthCheck.GetType().Name);
    }

    [Fact]
    public void HealthCheck_Factory_Builds_From_Keyed_Options()
    {
        const string key = "primary";
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database", serviceKey: key,
            configureSettings: s => s.HealthChecks.Enabled = true,
            configureOptions: o => o.ConnectionString = DummyConnectionString);

        var app = builder.Build();
        var registration = Assert.Single(GetRegistrations(app));

        // Factory must resolve the keyed options; a default (non-keyed) lookup would see a blank
        // connection string and GetRequiredConnectionString would throw InvalidOperationException.
        var healthCheck = registration.Factory(app.Services);
        Assert.Equal("SimpleSqlServerHealthCheck", healthCheck.GetType().Name);
    }

    [Fact]
    public void HealthCheck_Factory_Throws_When_ConnectionString_Missing()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        // Health checks enabled but NO connection string configured.
        builder.IgniteSqlServerClient("database",
            configureSettings: s => s.HealthChecks.Enabled = true);

        var app = builder.Build();
        var registration = Assert.Single(GetRegistrations(app));

        // The registration factory calls GetRequiredConnectionString, which must fail fast.
        var ex = Assert.Throws<InvalidOperationException>(() => registration.Factory(app.Services));
        Assert.Contains("ConnectionString is missing", ex.Message);
    }
}
