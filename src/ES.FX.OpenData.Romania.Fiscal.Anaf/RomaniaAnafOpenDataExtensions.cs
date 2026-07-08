using System.Net.Http.Headers;
using ES.FX.OpenData.Romania.Fiscal.Anaf;
using ES.FX.OpenData.Romania.Fiscal.Anaf.Internal;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ES.FX.OpenData;

/// <summary>Registration for the Romanian ANAF VAT-payer registry client.</summary>
[PublicAPI]
public static class RomaniaAnafOpenDataExtensions
{
    private const string HttpClientName = "ES.FX.OpenData.Romania.Fiscal.Anaf";

    /// <summary>
    ///     Registers <see cref="IAnafClient" /> as a singleton over <see cref="IHttpClientFactory" /> (safe to
    ///     inject into consumers of any lifetime), together with a process-wide request throttle (default ~1 req/s
    ///     — ANAF throttles per source IP). No resilience handler is applied by default; chain one on the returned
    ///     builder via <paramref name="configureHttpClient" />.
    /// </summary>
    /// <remarks>
    ///     Note: ANAF's lookup is an idempotent query performed via <c>POST</c>. Do not disable retries for unsafe
    ///     HTTP methods on this client if you add a resilience handler.
    /// </remarks>
    /// <param name="builder">The OpenData builder.</param>
    /// <param name="configureOptions">Optional options configuration (base URL, throughput, batch size).</param>
    /// <param name="configureHttpClient">Optional access to the underlying <see cref="IHttpClientBuilder" />.</param>
    public static IOpenDataBuilder AddRomaniaAnaf(this IOpenDataBuilder builder,
        Action<AnafClientOptions>? configureOptions = null,
        Action<IHttpClientBuilder>? configureHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;

        var optionsBuilder = services.AddOptions<AnafClientOptions>().ValidateOnStart();
        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<AnafClientOptions>, AnafClientOptionsValidator>());

        services.TryAddSingleton(serviceProvider =>
            new AnafRequestThrottle(CurrentOptions(serviceProvider).RequestInterval));

        var httpClientBuilder = services.AddHttpClient(HttpClientName, (serviceProvider, httpClient) =>
        {
            httpClient.BaseAddress = CurrentOptions(serviceProvider).GetBaseAddress();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        configureHttpClient?.Invoke(httpClientBuilder);

        services.TryAddSingleton<IAnafClient>(serviceProvider => new AnafClient(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            HttpClientName,
            serviceProvider.GetRequiredService<AnafRequestThrottle>(),
            CurrentOptions(serviceProvider)));

        return builder;
    }

    private static AnafClientOptions CurrentOptions(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptionsMonitor<AnafClientOptions>>().Get(Options.DefaultName);
}
