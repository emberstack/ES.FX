using JetBrains.Annotations;

namespace ES.FX.OpenData.Vies;

/// <summary>Options for the VIES client.</summary>
[PublicAPI]
public sealed class ViesClientOptions
{
    /// <summary>The base URL of the VIES REST API. Defaults to the official EU endpoint.</summary>
    public string BaseUrl { get; set; } = "https://ec.europa.eu/taxation_customs/vies/rest-api/";

    /// <summary>Resolves the base address (with a trailing slash so relative request URIs compose).</summary>
    public Uri GetBaseAddress()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("A VIES BaseUrl must be configured.");
        var baseUrl = BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/";
        return new Uri(baseUrl, UriKind.Absolute);
    }
}
