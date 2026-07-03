using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests;

/// <summary>
///     Tests that assert the observability wiring (health checks, tracing) and the SqlServer options
///     configuration branches (retry, timeout override, named instance) produced by the Spark. These are
///     pure DI/build assertions and require no database.
/// </summary>
public class ObservabilityAndOptionsTests
{
    private const string DbContextSparkName = "DbContext";

    /// <summary>
    ///     Resolves whether the built context's execution strategy retries on failure. This is the public,
    ///     observable outcome of the <c>EnableRetryOnFailure()</c> branch: a retrying strategy is produced
    ///     when retry is enabled, a non-retrying one when it is disabled.
    /// </summary>
    private static bool ContextRetriesOnFailure(IHost app)
    {
        var context = app.Services.GetRequiredService<TestDbContext>();
        return context.Database.CreateExecutionStrategy().RetriesOnFailure;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HealthCheck_Registered_WithExpectedName_Tags_And_FailureStatus(bool useFactory)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        // Enabled by default; add a custom tag + failure status via settings.
        void ConfigureSettings(SqlServerDbContextSparkSettings<TestDbContext> settings)
        {
            settings.HealthChecks.Enabled = true;
            settings.HealthChecks.FailureStatus = HealthStatus.Degraded;
            settings.HealthChecks.Tags = ["custom-tag"];
        }

        if (useFactory)
            builder.IgniteSqlServerDbContextFactory<TestDbContext>(configureSettings: ConfigureSettings);
        else
            builder.IgniteSqlServerDbContext<TestDbContext>(configureSettings: ConfigureSettings);

        var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var expectedName = $"{DbContextSparkName}.{nameof(TestDbContext)}";
        var registration = Assert.Single(options.Registrations, r => r.Name == expectedName);

        // Failure status flows through from settings.
        Assert.Equal(HealthStatus.Degraded, registration.FailureStatus);

        // The spark always prepends its own name tag, then appends the configured tags.
        Assert.Contains(DbContextSparkName, registration.Tags);
        Assert.Contains("custom-tag", registration.Tags);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HealthCheck_NotRegistered_WhenDisabled(bool useFactory)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        void ConfigureSettings(SqlServerDbContextSparkSettings<TestDbContext> settings) =>
            settings.HealthChecks.Enabled = false;

        if (useFactory)
            builder.IgniteSqlServerDbContextFactory<TestDbContext>(configureSettings: ConfigureSettings);
        else
            builder.IgniteSqlServerDbContext<TestDbContext>(configureSettings: ConfigureSettings);

        var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var expectedName = $"{DbContextSparkName}.{nameof(TestDbContext)}";
        Assert.DoesNotContain(options.Registrations, r => r.Name == expectedName);
    }

    [Fact]
    public void HealthCheck_Timeout_Applied_ToMatchingRegistration()
    {
        var timeout = TimeSpan.FromSeconds(17);

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerDbContext<TestDbContext>(configureSettings: settings =>
        {
            settings.HealthChecks.Enabled = true;
            settings.HealthChecks.Timeout = timeout;
        });

        var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var expectedName = $"{DbContextSparkName}.{nameof(TestDbContext)}";
        var registration = Assert.Single(options.Registrations, r => r.Name == expectedName);
        Assert.Equal(timeout, registration.Timeout);
    }

    [Fact]
    public void HealthCheck_Timeout_NotSet_LeavesDefaultTimeout()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerDbContext<TestDbContext>(configureSettings: settings =>
        {
            settings.HealthChecks.Enabled = true;
            // Timeout intentionally left null.
        });

        var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var expectedName = $"{DbContextSparkName}.{nameof(TestDbContext)}";
        var registration = Assert.Single(options.Registrations, r => r.Name == expectedName);

        // The health-checks default timeout is System.Threading.Timeout.InfiniteTimeSpan (-1 ms).
        Assert.Equal(System.Threading.Timeout.InfiniteTimeSpan, registration.Timeout);
    }

    [Fact]
    public void Retry_EnabledByDefault_ProducesRetryingExecutionStrategy()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        // Default DisableRetry == false => EnableRetryOnFailure() should be called.
        builder.IgniteSqlServerDbContext<TestDbContext>();

        var app = builder.Build();

        Assert.True(ContextRetriesOnFailure(app));
    }

    [Fact]
    public void Retry_Disabled_ProducesNonRetryingExecutionStrategy()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{nameof(TestDbContext)}:{nameof(SqlServerDbContextSparkOptions<>.DisableRetry)}",
                true.ToString())
        ]);

        builder.IgniteSqlServerDbContext<TestDbContext>();

        var app = builder.Build();

        Assert.False(ContextRetriesOnFailure(app));
    }

    [Fact]
    public void Tracing_Enabled_RegistersTracerProvider()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        // Tracing is enabled by default.
        builder.IgniteSqlServerDbContext<TestDbContext>();

        var app = builder.Build();

        // The spark calls AddOpenTelemetry().WithTracing(...) which registers a TracerProvider in DI.
        var tracerProvider = app.Services.GetService<TracerProvider>();
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void Tracing_Disabled_DoesNotRegisterTracerProvider()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerDbContext<TestDbContext>(configureSettings: settings =>
            settings.Tracing.Enabled = false);

        var app = builder.Build();

        // With tracing disabled the spark never touches AddOpenTelemetry, so no TracerProvider is present.
        var tracerProvider = app.Services.GetService<TracerProvider>();
        Assert.Null(tracerProvider);
    }

    [Fact]
    public void NamedInstance_BindsNamedConfigSection_AndProducesNamedHealthCheck()
    {
        const string name = "PrimaryDb";
        const int commandTimeout = 4242;

        var builder = Host.CreateEmptyApplicationBuilder(null);

        // Configuration is keyed by the explicit name, not by the DbContext type name.
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{name}:{nameof(SqlServerDbContextSparkOptions<>.CommandTimeout)}",
                commandTimeout.ToString()),
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{name}:{SparkConfig.Settings}:{nameof(SqlServerDbContextSparkSettings<TestDbContext>.HealthChecks)}:{nameof(HealthCheckSettings.Enabled)}",
                true.ToString())
        ]);

        builder.IgniteSqlServerDbContext<TestDbContext>(name);

        var app = builder.Build();

        // Options were bound from the named section.
        var resolvedOptions =
            app.Services.GetRequiredService<IOptions<SqlServerDbContextSparkOptions<TestDbContext>>>();
        Assert.Equal(commandTimeout, resolvedOptions.Value.CommandTimeout);

        // The health check name uses the explicit name, not the type name.
        var healthOptions = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var expectedName = $"{DbContextSparkName}.{name}";
        Assert.Single(healthOptions.Registrations, r => r.Name == expectedName);
        Assert.DoesNotContain(healthOptions.Registrations,
            r => r.Name == $"{DbContextSparkName}.{nameof(TestDbContext)}");
    }

    [Fact]
    public void NamedInstance_SettingsBoundFromNamedSection()
    {
        const string name = "SecondaryDb";

        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{name}:{SparkConfig.Settings}:{nameof(SqlServerDbContextSparkSettings<TestDbContext>.Tracing)}:{nameof(TracingSettings.Enabled)}",
                false.ToString())
        ]);

        builder.IgniteSqlServerDbContext<TestDbContext>(name);

        var app = builder.Build();

        var resolvedSettings = app.Services.GetRequiredService<SqlServerDbContextSparkSettings<TestDbContext>>();
        Assert.False(resolvedSettings.Tracing.Enabled);
    }
}
