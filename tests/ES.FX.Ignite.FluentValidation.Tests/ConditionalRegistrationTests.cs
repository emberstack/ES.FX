using ES.FX.Ignite.FluentValidation.Configuration;
using ES.FX.Ignite.FluentValidation.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Configuration;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Results;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Configuration;

namespace ES.FX.Ignite.FluentValidation.Tests;

/// <summary>
///     Tests that exercise the conditional registration branches of
///     <see cref="FluentValidationHostingExtensions.IgniteFluentValidation" />: each of the two
///     AutoValidation feature flags (<see cref="FluentValidationSparkSettings.EndpointsAutoValidationEnabled" />
///     and <see cref="FluentValidationSparkSettings.MvcAutoValidationEnabled" />) is toggled independently and the
///     corresponding SharpGrip registrations are asserted present/absent so an inverted or dropped if-branch fails.
/// </summary>
public class ConditionalRegistrationTests
{
    private const string EndpointsEnabledKey =
        $"{FluentValidationSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:" +
        nameof(FluentValidationSparkSettings.EndpointsAutoValidationEnabled);

    private const string MvcEnabledKey =
        $"{FluentValidationSpark.ConfigurationSectionPath}:{SparkConfig.Settings}:" +
        nameof(FluentValidationSparkSettings.MvcAutoValidationEnabled);

    // Namespace roots used to distinguish which SharpGrip branch registered a given service type.
    private const string EndpointsNamespace = "SharpGrip.FluentValidation.AutoValidation.Endpoints";
    private const string MvcNamespace = "SharpGrip.FluentValidation.AutoValidation.Mvc";

    private static IHostApplicationBuilder BuildWith(bool endpointsEnabled, bool mvcEnabled,
        Action<AutoValidationEndpointsConfiguration>? configureEndpoints = null,
        Action<AutoValidationMvcConfiguration>? configureMvc = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(EndpointsEnabledKey, endpointsEnabled.ToString()),
            new KeyValuePair<string, string?>(MvcEnabledKey, mvcEnabled.ToString())
        ]);
        builder.IgniteFluentValidation(
            configureAutoValidationEndpointsConfiguration: configureEndpoints,
            configureAutoValidationMvcConfiguration: configureMvc);
        return builder;
    }

    private static bool HasServiceFromNamespace(IServiceCollection services, string namespaceRoot) =>
        services.Any(descriptor =>
            (descriptor.ServiceType.Namespace?.StartsWith(namespaceRoot, StringComparison.Ordinal) ?? false) ||
            (descriptor.ImplementationType?.Namespace?.StartsWith(namespaceRoot, StringComparison.Ordinal) ?? false));

    [Fact]
    public void EndpointsDisabled_DoesNotRegister_EndpointsAutoValidation()
    {
        var builder = BuildWith(false, false);

        // The settings singleton must still be registered regardless of the feature flags.
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(FluentValidationSparkSettings));

        // No endpoints-specific service (e.g. IFluentValidationAutoValidationResultFactory) should be present.
        Assert.DoesNotContain(builder.Services,
            d => d.ServiceType == typeof(IFluentValidationAutoValidationResultFactory));
        Assert.False(HasServiceFromNamespace(builder.Services, EndpointsNamespace));
    }

    [Fact]
    public void EndpointsEnabled_Registers_EndpointsAutoValidation()
    {
        var builder = BuildWith(true, false);

        Assert.Contains(builder.Services,
            d => d.ServiceType == typeof(IFluentValidationAutoValidationResultFactory));
        Assert.True(HasServiceFromNamespace(builder.Services, EndpointsNamespace));
    }

    [Fact]
    public void MvcDisabled_DoesNotRegister_MvcAutoValidation()
    {
        // Enable only endpoints so any MVC-namespaced service can only come from the MVC branch.
        var builder = BuildWith(true, false);

        Assert.False(HasServiceFromNamespace(builder.Services, MvcNamespace));
    }

    [Fact]
    public void MvcEnabled_Registers_MvcAutoValidation()
    {
        // Enable only MVC so any MVC-namespaced service isolates the MVC branch from the endpoints branch.
        var builder = BuildWith(false, true);

        Assert.True(HasServiceFromNamespace(builder.Services, MvcNamespace));

        // The endpoints branch is off, so the endpoints result factory must NOT be present. This isolates the
        // MVC-specific registration from the endpoints-specific one.
        Assert.DoesNotContain(builder.Services,
            d => d.ServiceType == typeof(IFluentValidationAutoValidationResultFactory));
        Assert.False(HasServiceFromNamespace(builder.Services, EndpointsNamespace));
    }

    [Fact]
    public void BothDisabled_RegistersOnlySettings_NoAutoValidation()
    {
        var builder = BuildWith(false, false);

        Assert.Contains(builder.Services, d => d.ServiceType == typeof(FluentValidationSparkSettings));
        Assert.False(HasServiceFromNamespace(builder.Services, EndpointsNamespace));
        Assert.False(HasServiceFromNamespace(builder.Services, MvcNamespace));
        Assert.DoesNotContain(builder.Services,
            d => d.ServiceType == typeof(IFluentValidationAutoValidationResultFactory));
    }

    [Fact]
    public void BothEnabled_RegistersBothBranches()
    {
        var builder = BuildWith(true, true);

        Assert.True(HasServiceFromNamespace(builder.Services, EndpointsNamespace));
        Assert.True(HasServiceFromNamespace(builder.Services, MvcNamespace));
        Assert.Contains(builder.Services,
            d => d.ServiceType == typeof(IFluentValidationAutoValidationResultFactory));
    }

    [Fact]
    public void EndpointsConfigurationDelegate_IsInvoked()
    {
        var invoked = false;

        AutoValidationEndpointsConfiguration? captured = null;
        var builder = BuildWith(true, false,
            config =>
            {
                invoked = true;
                captured = config;
            });

        // SharpGrip invokes the endpoints configuration delegate synchronously during registration and
        // passes a non-null configuration instance that customization code can mutate.
        Assert.True(invoked);
        Assert.NotNull(captured);
        _ = builder;
    }

    [Fact]
    public void MvcConfigurationDelegate_IsInvoked()
    {
        var invoked = false;

        AutoValidationMvcConfiguration? captured = null;
        var builder = BuildWith(false, true,
            configureMvc: config =>
            {
                invoked = true;
                captured = config;
            });

        // SharpGrip invokes the MVC configuration delegate synchronously during registration and passes a
        // non-null configuration instance that customization code can mutate.
        Assert.True(invoked);
        Assert.NotNull(captured);
        _ = builder;
    }

    [Fact]
    public void EndpointsConfigurationDelegate_NotInvoked_WhenEndpointsDisabled()
    {
        var invoked = false;

        BuildWith(false, true,
            _ => invoked = true);

        // The endpoints branch is skipped, so its configuration delegate must never run.
        Assert.False(invoked);
    }

    [Fact]
    public void MvcConfigurationDelegate_NotInvoked_WhenMvcDisabled()
    {
        var invoked = false;

        BuildWith(true, false,
            configureMvc: _ => invoked = true);

        // The MVC branch is skipped, so its configuration delegate must never run.
        Assert.False(invoked);
    }
}