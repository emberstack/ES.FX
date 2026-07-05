using System.Net.Http.Headers;
using ES.FX.Zendesk;
using ES.FX.Zendesk.Attachments;
using ES.FX.Zendesk.Authentication;
using ES.FX.Zendesk.Configuration;
using ES.FX.Zendesk.HelpCenter;
using ES.FX.Zendesk.Support;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

// ReSharper disable once CheckNamespace — DI extensions are conventionally in this namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods to register the Zendesk API clients (OAuth <c>client_credentials</c>) using
///     <see cref="IHttpClientFactory" />. Registers the generated <see cref="ZendeskSupportApiClient" /> (Support
///     API) and <see cref="ZendeskHelpCenterApiClient" /> (Help Center API) plus the curated
///     <see cref="ZendeskAttachmentContentFetcher" />. Supports multiple named/keyed instances (pass a distinct
///     <c>serviceKey</c> per instance).
/// </summary>
[PublicAPI]
public static class ZendeskClientServiceCollectionExtensions
{
    // Zendesk asks API clients to identify themselves with a descriptive User-Agent.
    private static readonly ProductInfoHeaderValue UserAgent = new(
        "ES.FX.Zendesk", typeof(ZendeskClientOptions).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");

    /// <summary>
    ///     Registers the Zendesk clients as keyed services together with the OAuth token provider and options
    ///     validation. Expects the corresponding named <see cref="ZendeskClientOptions" /> (name =
    ///     <paramref name="serviceKey" /> or the default) to be configured by the caller (e.g. bound from
    ///     configuration). Registered services (all keyed by <paramref name="serviceKey" />, or default when
    ///     <c>null</c>): <see cref="ZendeskSupportApiClient" />, <see cref="ZendeskHelpCenterApiClient" />,
    ///     <see cref="IRequestAdapter" /> (for advanced/raw requests) and
    ///     <see cref="ZendeskAttachmentContentFetcher" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" />.</param>
    /// <param name="serviceKey">
    ///     If not <c>null</c>, registers keyed instances retrievable via
    ///     <c>GetRequiredKeyedService&lt;T&gt;(serviceKey)</c>. If <c>null</c>, registers the default instances
    ///     retrievable via <c>GetRequiredService&lt;T&gt;()</c>.
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
        services.AddHttpClient(tokenClientName,
            httpClient => httpClient.DefaultRequestHeaders.UserAgent.Add(UserAgent));

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
                httpClient.DefaultRequestHeaders.UserAgent.Add(UserAgent);
            })
            // Registration order = outer-to-inner. The guard sits OUTSIDE the auth handler (but inside the
            // Ignite-provided resilience handler): the auth handler must see a raw 401 to perform its
            // one-shot token-refresh retry BEFORE the guard turns the final non-retryable failure into a
            // typed exception. Retryable statuses (408/429/5xx) pass through the guard untouched for the
            // resilience pipeline.
            .AddHttpMessageHandler(() => new ZendeskResponseGuardHandler())
            .AddHttpMessageHandler(serviceProvider =>
                new ZendeskAuthenticationDelegatingHandler(
                    serviceProvider.GetRequiredKeyedService<IZendeskAccessTokenProvider>(serviceKey)));

        // Transient (not singleton): resolves a fresh, factory-managed HttpClient per resolution so the pooled
        // handler chain rotates on its HandlerLifetime instead of being captured for the process lifetime.
        // Authentication happens in the HttpClient handler chain, so the adapter itself is anonymous. The
        // generated request templates carry the full /api/v2/… path, so the adapter targets the service root.
        services.AddKeyedTransient<IRequestAdapter>(serviceKey, (serviceProvider, _) =>
        {
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<ZendeskClientOptions>>()
                .Get(optionsName);
            var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(httpClientName);
            // Kiota emits request spans on its own fixed ActivitySource
            // (ZendeskClientInstrumentation.KiotaActivitySourceName) — the Spark subscribes to it.
            return new HttpClientRequestAdapter(
                new AnonymousAuthenticationProvider(),
                httpClient: httpClient)
            {
                BaseUrl = options.GetServiceRootAddress().ToString().TrimEnd('/')
            };
        });

        services.AddKeyedTransient(serviceKey, (serviceProvider, key) =>
            new ZendeskSupportApiClient(serviceProvider.GetRequiredKeyedService<IRequestAdapter>(key)));
        services.AddKeyedTransient(serviceKey, (serviceProvider, key) =>
            new ZendeskHelpCenterApiClient(serviceProvider.GetRequiredKeyedService<IRequestAdapter>(key)));

        services.AddKeyedTransient(serviceKey, (serviceProvider, _) =>
            new ZendeskAttachmentContentFetcher(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName)));

        return httpClientBuilder;
    }

    /// <summary>
    ///     Registers the Zendesk clients and configures the named <see cref="ZendeskClientOptions" /> inline.
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