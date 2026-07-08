using System.Net.Http.Headers;
using ES.FX.OpenData.Vies;
using ES.FX.OpenData.Vies.Internal;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ES.FX.OpenData;

/// <summary>Registration for the EU VIES VAT-validation client.</summary>
[PublicAPI]
public static class ViesOpenDataExtensions
{
    private const string HttpClientName = "ES.FX.OpenData.Vies";

    /// <summary>
    ///     Registers <see cref="IViesClient" /> as a singleton over <see cref="IHttpClientFactory" /> (safe to
    ///     inject into consumers of any lifetime). No resilience handler is applied by default — chain one on the
    ///     returned builder via <paramref name="configureHttpClient" />, or rely on your host's defaults.
    /// </summary>
    /// <param name="builder">The OpenData builder.</param>
    /// <param name="configureOptions">Optional options configuration (base URL).</param>
    /// <param name="configureHttpClient">Optional access to the underlying <see cref="IHttpClientBuilder" />.</param>
    public static IOpenDataBuilder AddVies(this IOpenDataBuilder builder,
        Action<ViesClientOptions>? configureOptions = null,
        Action<IHttpClientBuilder>? configureHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        var optionsBuilder = services.AddOptions<ViesClientOptions>().ValidateOnStart();
        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ViesClientOptions>, ViesClientOptionsValidator>());

        var httpClientBuilder = services.AddHttpClient(HttpClientName, (serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<ViesClientOptions>>()
                .Get(Options.DefaultName);
            httpClient.BaseAddress = options.GetBaseAddress();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        configureHttpClient?.Invoke(httpClientBuilder);

        services.TryAddSingleton<IViesClient>(serviceProvider =>
            new ViesClient(serviceProvider.GetRequiredService<IHttpClientFactory>(), HttpClientName));

        return builder;
    }
}
