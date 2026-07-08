using ES.FX.OpenData.Internal;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ES.FX.OpenData;

/// <summary>Registration entry point for the OpenData family.</summary>
[PublicAPI]
public static class OpenDataServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the OpenData hub and startup warmup service, returning a builder onto which dataset and
    ///     client packages chain their own <c>Add…</c> methods. Idempotent — safe to call more than once.
    /// </summary>
    public static IOpenDataBuilder AddOpenData(this IServiceCollection services,
        Action<OpenDataOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new OpenDataOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IOpenData, OpenDataHub>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OpenDataWarmupService>());

        return new OpenDataBuilder(services);
    }
}
