using Microsoft.Extensions.Options;

namespace ES.FX.OpenData.Romania.Anaf.VatCheck.Internal;

internal sealed class AnafVatCheckClientOptionsValidator : IValidateOptions<AnafVatCheckClientOptions>
{
    public ValidateOptionsResult Validate(string? name, AnafVatCheckClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            return ValidateOptionsResult.Fail("An ANAF BaseUrl must be configured.");

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return ValidateOptionsResult.Fail($"The ANAF BaseUrl '{options.BaseUrl}' must be an absolute http(s) URL.");

        if (options.RequestsPerSecond < 0)
            return ValidateOptionsResult.Fail("ANAF RequestsPerSecond must be zero (disabled) or positive.");

        if (options.BatchSize is < 1 or > 100)
            return ValidateOptionsResult.Fail(
                "ANAF BatchSize must be between 1 and 100 (ANAF's documented hard limit of 100 CUIs per request).");

        return ValidateOptionsResult.Success;
    }
}