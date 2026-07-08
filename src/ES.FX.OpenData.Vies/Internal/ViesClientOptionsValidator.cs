using Microsoft.Extensions.Options;

namespace ES.FX.OpenData.Vies.Internal;

internal sealed class ViesClientOptionsValidator : IValidateOptions<ViesClientOptions>
{
    public ValidateOptionsResult Validate(string? name, ViesClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            return ValidateOptionsResult.Fail("A VIES BaseUrl must be configured.");

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return ValidateOptionsResult.Fail($"The VIES BaseUrl '{options.BaseUrl}' must be an absolute http(s) URL.");

        return ValidateOptionsResult.Success;
    }
}
