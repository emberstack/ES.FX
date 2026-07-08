using JetBrains.Annotations;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf;

/// <summary>Options for the ANAF client.</summary>
[PublicAPI]
public sealed class AnafClientOptions
{
    /// <summary>The base URL of the ANAF web services. Defaults to the official endpoint.</summary>
    public string BaseUrl { get; set; } = "https://webservicesp.anaf.ro/";

    /// <summary>
    ///     The client-side request budget, in requests per second, shared across all consumers in the process.
    ///     ANAF throttles per source IP (~1 req/s); set to <c>0</c> to disable the built-in throttle. Default: 1.
    /// </summary>
    public int RequestsPerSecond { get; set; } = 1;

    /// <summary>The maximum number of CUIs sent per request; larger batches are chunked. Default: 100.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>The minimum interval between requests derived from <see cref="RequestsPerSecond" />.</summary>
    public TimeSpan RequestInterval =>
        RequestsPerSecond > 0 ? TimeSpan.FromSeconds(1.0 / RequestsPerSecond) : TimeSpan.Zero;

    /// <summary>The effective, always-positive batch size.</summary>
    public int EffectiveBatchSize => BatchSize > 0 ? BatchSize : 100;

    /// <summary>Resolves the base address (with a trailing slash so relative request URIs compose).</summary>
    public Uri GetBaseAddress()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("An ANAF BaseUrl must be configured.");
        var baseUrl = BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/";
        return new Uri(baseUrl, UriKind.Absolute);
    }
}
