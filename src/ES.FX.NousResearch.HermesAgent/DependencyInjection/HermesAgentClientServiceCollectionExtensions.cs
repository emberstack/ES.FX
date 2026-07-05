using System.Net.Http.Headers;
using ES.FX.NousResearch.HermesAgent;
using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Authentication;
using ES.FX.NousResearch.HermesAgent.Configuration;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace — DI extensions are conventionally in this namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods to register the Hermes Agent API client (static bearer key) using
///     <see cref="IHttpClientFactory" />. Supports multiple named/keyed instances (pass a distinct
///     <c>serviceKey</c> per instance).
/// </summary>
[PublicAPI]
public static class HermesAgentClientServiceCollectionExtensions
{
    // Identify the client to the server with a descriptive User-Agent.
    private static readonly ProductInfoHeaderValue UserAgent = new(
        "ES.FX.NousResearch.HermesAgent",
        typeof(HermesAgentClient).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");

    /// <summary>
    ///     Registers <see cref="IHermesAgentClient" /> as a typed <see cref="HttpClient" /> together with the
    ///     bearer authentication handler and options validation. Expects the corresponding named
    ///     <see cref="HermesAgentClientOptions" /> (name = <paramref name="serviceKey" /> or the default) to be
    ///     configured by the caller (bound from configuration and/or via <paramref name="configureOptions" />).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" />.</param>
    /// <param name="serviceKey">
    ///     If not <c>null</c>, registers a keyed instance retrievable via
    ///     <c>GetRequiredKeyedService&lt;IHermesAgentClient&gt;(serviceKey)</c>. If <c>null</c>, registers the
    ///     default instance retrievable via <c>GetRequiredService&lt;IHermesAgentClient&gt;()</c>.
    /// </param>
    /// <param name="configureOptions">An optional delegate to configure the named options inline.</param>
    /// <returns>The <see cref="IHttpClientBuilder" /> for the underlying named client, for further customization.</returns>
    public static IHttpClientBuilder AddHermesAgentClient(this IServiceCollection services,
        string? serviceKey = null, Action<HermesAgentClientOptions>? configureOptions = null)
    {
        var optionsName = serviceKey ?? string.Empty; // string.Empty == Options.DefaultName
        var httpClientName = HttpClientName(serviceKey);

        var optionsBuilder = services.AddOptions<HermesAgentClientOptions>(optionsName);
        if (configureOptions is not null) optionsBuilder.Configure(configureOptions);

        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IValidateOptions<HermesAgentClientOptions>, HermesAgentClientOptionsValidator>());

        var httpClientBuilder = services
            .AddHttpClient(httpClientName, (serviceProvider, httpClient) =>
            {
                var options = serviceProvider.GetRequiredService<IOptionsMonitor<HermesAgentClientOptions>>()
                    .Get(optionsName);
                httpClient.BaseAddress = options.GetBaseAddress();
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.UserAgent.Add(UserAgent);
            })
            .AddHttpMessageHandler(serviceProvider =>
                new HermesAgentAuthenticationDelegatingHandler(
                    serviceProvider.GetRequiredService<IOptionsMonitor<HermesAgentClientOptions>>(),
                    optionsName));

        // Transient (not singleton): resolves a fresh, factory-managed HttpClient per resolution so the pooled
        // handler chain rotates on its HandlerLifetime instead of being captured for the process lifetime.
        services.AddKeyedTransient<IHermesAgentClient>(serviceKey, (serviceProvider, _) =>
            new HermesAgentClient(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName),
                serviceProvider.GetRequiredService<ILoggerFactory>()));

        return httpClientBuilder;
    }

    private static string HttpClientName(string? serviceKey) =>
        serviceKey is null ? "ES.FX.NousResearch.HermesAgent" : $"ES.FX.NousResearch.HermesAgent[{serviceKey}]";
}