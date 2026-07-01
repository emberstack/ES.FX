using System.Net.Http.Headers;
using ES.FX.Zendesk;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Authentication;
using ES.FX.Zendesk.Configuration;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace — DI extensions are conventionally in this namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods to register the Zendesk API client (OAuth <c>client_credentials</c>) using
///     <see cref="IHttpClientFactory" />. Supports multiple named/keyed instances (pass a distinct
///     <c>serviceKey</c> per instance).
/// </summary>
[PublicAPI]
public static class ZendeskClientServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <see cref="IZendeskClient" /> as a typed <see cref="HttpClient" /> together with the OAuth
    ///     token provider and options validation. Expects the corresponding named
    ///     <see cref="ZendeskClientOptions" /> (name = <paramref name="serviceKey" /> or the default) to be
    ///     configured by the caller (e.g. bound from configuration).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" />.</param>
    /// <param name="serviceKey">
    ///     If not <c>null</c>, registers a keyed instance retrievable via
    ///     <c>GetRequiredKeyedService&lt;IZendeskClient&gt;(serviceKey)</c>. If <c>null</c>, registers the default
    ///     instance retrievable via <c>GetRequiredService&lt;IZendeskClient&gt;()</c>.
    /// </param>
    /// <returns>The <see cref="IHttpClientBuilder" /> for the underlying named client, for further customization.</returns>
    public static IHttpClientBuilder AddZendeskClient(this IServiceCollection services, string? serviceKey = null)
    {
        var optionsName = serviceKey ?? string.Empty; // string.Empty == Options.DefaultName
        var httpClientName = HttpClientName(serviceKey);
        var tokenClientName = httpClientName + ".Token";

        services.AddOptions<ZendeskClientOptions>(optionsName);
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<ZendeskClientOptions>, ZendeskClientOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);

        // Dedicated token-endpoint client — has NO auth handler, so acquiring a token does not recurse.
        services.AddHttpClient(tokenClientName);

        // OAuth token provider (singleton per instance: owns the token cache + refresh lock).
        services.AddKeyedSingleton<IZendeskAccessTokenProvider>(serviceKey, (serviceProvider, _) =>
            new ClientCredentialsAccessTokenProvider(
                serviceProvider.GetRequiredService<IHttpClientFactory>(),
                tokenClientName,
                serviceProvider.GetRequiredService<IOptionsMonitor<ZendeskClientOptions>>(),
                optionsName,
                serviceProvider.GetRequiredService<TimeProvider>(),
                serviceProvider.GetRequiredService<ILogger<ClientCredentialsAccessTokenProvider>>()));

        var httpClientBuilder = services
            .AddHttpClient(httpClientName, (serviceProvider, httpClient) =>
            {
                var options = serviceProvider.GetRequiredService<IOptionsMonitor<ZendeskClientOptions>>()
                    .Get(optionsName);
                httpClient.BaseAddress = options.GetBaseAddress();
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddHttpMessageHandler(serviceProvider =>
                new ZendeskAuthenticationDelegatingHandler(
                    serviceProvider.GetRequiredKeyedService<IZendeskAccessTokenProvider>(serviceKey)));

        // Transient (not singleton): resolves a fresh, factory-managed HttpClient per resolution so the pooled
        // handler chain rotates on its HandlerLifetime instead of being captured for the process lifetime.
        services.AddKeyedTransient<IZendeskClient>(serviceKey, (serviceProvider, _) =>
            new ZendeskClient(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName),
                serviceProvider.GetRequiredService<ILoggerFactory>()));

        return httpClientBuilder;
    }

    /// <summary>
    ///     Registers <see cref="IZendeskClient" /> and configures the named <see cref="ZendeskClientOptions" />
    ///     inline.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" />.</param>
    /// <param name="configureOptions">A delegate to configure the options.</param>
    /// <param name="serviceKey">The instance key (see the other overload). Defaults to the default instance.</param>
    /// <returns>The <see cref="IHttpClientBuilder" /> for the underlying named client, for further customization.</returns>
    public static IHttpClientBuilder AddZendeskClient(this IServiceCollection services,
        Action<ZendeskClientOptions> configureOptions, string? serviceKey = null)
    {
        services.AddOptions<ZendeskClientOptions>(serviceKey ?? string.Empty)
            .Configure(configureOptions);
        return services.AddZendeskClient(serviceKey);
    }

    private static string HttpClientName(string? serviceKey) =>
        serviceKey is null ? "ES.FX.Zendesk" : $"ES.FX.Zendesk[{serviceKey}]";
}