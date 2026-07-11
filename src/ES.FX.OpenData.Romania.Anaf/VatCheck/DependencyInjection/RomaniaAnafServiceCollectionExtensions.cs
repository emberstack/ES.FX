using System.Net.Http.Headers;
using ES.FX.OpenData.Romania.Anaf.VatCheck;
using ES.FX.OpenData.Romania.Anaf.VatCheck.Internal;
using ES.FX.OpenData.Romania.TerritorialUnits;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace — DI registration extensions are conventionally in this namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registration for the Romanian ANAF VAT-payer registry client.</summary>
[PublicAPI]
public static class RomaniaAnafServiceCollectionExtensions
{
    private const string HttpClientName = "ES.FX.OpenData.Romania.Anaf.VatCheck";

    /// <summary>
    ///     Registers <see cref="IAnafVatCheckClient" /> as a singleton over <see cref="IHttpClientFactory" /> (safe to
    ///     inject into consumers of any lifetime), together with a process-wide request throttle (default 1 req/s,
    ///     ANAF's documented per-client limit). No resilience handler is applied by default; chain one on the returned
    ///     builder via <paramref name="configureHttpClient" />.
    /// </summary>
    /// <remarks>
    ///     Note: ANAF's lookup is an idempotent query performed via <c>POST</c>. Do not disable retries for unsafe
    ///     HTTP methods on this client if you add a resilience handler.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional options configuration (base URL, throughput, batch size).</param>
    /// <param name="configureHttpClient">Optional access to the underlying <see cref="IHttpClientBuilder" />.</param>
    public static IServiceCollection AddRomaniaAnaf(this IServiceCollection services,
        Action<AnafVatCheckClientOptions>? configureOptions = null,
        Action<IHttpClientBuilder>? configureHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddRomaniaTerritorialUnits(); // the client resolves address counties to SIRUTA
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IAnafVatCheckClient))) return services;

        var optionsBuilder = services.AddOptions<AnafVatCheckClientOptions>().ValidateOnStart();
        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);
        services.TryAddEnumerable(
            ServiceDescriptor
                .Singleton<IValidateOptions<AnafVatCheckClientOptions>, AnafVatCheckClientOptionsValidator>());

        services.TryAddSingleton(serviceProvider =>
            new AnafRequestThrottle(CurrentOptions(serviceProvider).RequestInterval));

        services.TryAddSingleton<AnafSirutaCrosswalk>();

        var httpClientBuilder = services.AddHttpClient(HttpClientName, (serviceProvider, httpClient) =>
        {
            httpClient.BaseAddress = CurrentOptions(serviceProvider).GetBaseAddress();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        configureHttpClient?.Invoke(httpClientBuilder);

        services.TryAddSingleton<IAnafVatCheckClient>(serviceProvider => new AnafVatCheckClient(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            HttpClientName,
            serviceProvider.GetRequiredService<AnafRequestThrottle>(),
            CurrentOptions(serviceProvider),
            serviceProvider.GetRequiredService<IRomanianTerritorialUnitsDataset>(),
            serviceProvider.GetRequiredService<AnafSirutaCrosswalk>()));

        return services;
    }

    private static AnafVatCheckClientOptions CurrentOptions(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptionsMonitor<AnafVatCheckClientOptions>>()
            .Get(Options.Options.DefaultName);
}