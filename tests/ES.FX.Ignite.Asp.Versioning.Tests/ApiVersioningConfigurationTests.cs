using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using ES.FX.Ignite.Asp.Versioning.Hosting;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.Asp.Versioning.Tests;

public class ApiVersioningConfigurationTests
{
    private static ApiVersioningOptions ResolveApiVersioningOptions(IHost host) =>
        host.Services.GetRequiredService<IOptions<ApiVersioningOptions>>().Value;

    private static ApiExplorerOptions ResolveApiExplorerOptions(IHost host) =>
        host.Services.GetRequiredService<IOptions<ApiExplorerOptions>>().Value;

    [Fact]
    public void Defaults_ApiVersioningOptions_ReportApiVersions_And_UrlSegmentReader()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteApiVersioning();
        using var host = builder.Build();

        var options = ResolveApiVersioningOptions(host);

        Assert.True(options.ReportApiVersions);
        Assert.IsType<UrlSegmentApiVersionReader>(options.ApiVersionReader);
    }

    [Fact]
    public void Defaults_ApiExplorerOptions_GroupNameFormat_And_SubstituteApiVersionInUrl()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteApiVersioning();
        using var host = builder.Build();

        var options = ResolveApiExplorerOptions(host);

        // ReSharper disable once StringLiteralTypo
        Assert.Equal("'v'VVV", options.GroupNameFormat);
        Assert.True(options.SubstituteApiVersionInUrl);
    }

    [Fact]
    public void ConfigureApiVersioningOptions_Delegate_Runs_And_Applies_After_Defaults()
    {
        var delegateInvoked = false;
        var reportApiVersionsAtInvocation = (bool?)null;
        var readerAtInvocation = (IApiVersionReader?)null;
        var customReader = new HeaderApiVersionReader("api-version");

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteApiVersioning(options =>
        {
            delegateInvoked = true;
            // Capture the state at invocation to prove the defaults were applied *before* the delegate.
            reportApiVersionsAtInvocation = options.ReportApiVersions;
            readerAtInvocation = options.ApiVersionReader;

            // Override a default to prove the delegate wins (runs after defaults).
            options.ReportApiVersions = false;
            options.ApiVersionReader = customReader;
        });
        using var host = builder.Build();

        var options = ResolveApiVersioningOptions(host);

        Assert.True(delegateInvoked);
        // At the time the delegate ran, the opinionated defaults were already in place.
        Assert.True(reportApiVersionsAtInvocation);
        Assert.IsType<UrlSegmentApiVersionReader>(readerAtInvocation);
        // Final resolved values reflect the delegate's overrides.
        Assert.False(options.ReportApiVersions);
        Assert.Same(customReader, options.ApiVersionReader);
    }

    [Fact]
    public void ConfigureApiExplorerOptions_Delegate_Runs_And_Applies_After_Defaults()
    {
        var delegateInvoked = false;
        var groupNameFormatAtInvocation = (string?)null;
        var substituteAtInvocation = (bool?)null;

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteApiVersioning(configureApiExplorerOptions: options =>
        {
            delegateInvoked = true;
            groupNameFormatAtInvocation = options.GroupNameFormat;
            substituteAtInvocation = options.SubstituteApiVersionInUrl;

            // Override the defaults to prove the delegate runs after them.
            options.GroupNameFormat = "VVVV";
            options.SubstituteApiVersionInUrl = false;
        });
        using var host = builder.Build();

        var options = ResolveApiExplorerOptions(host);

        Assert.True(delegateInvoked);
        // ReSharper disable once StringLiteralTypo
        Assert.Equal("'v'VVV", groupNameFormatAtInvocation);
        Assert.True(substituteAtInvocation);
        Assert.Equal("VVVV", options.GroupNameFormat);
        Assert.False(options.SubstituteApiVersionInUrl);
    }

    [Fact]
    public void IgniteApiVersioning_CalledTwice_Throws_ReconfigurationNotSupportedException()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteApiVersioning();

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteApiVersioning());
    }
}