using Microsoft.Extensions.Options;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf.Internal;

internal sealed class AnafClientOptionsValidator : IValidateOptions<AnafClientOptions>
{
    public ValidateOptionsResult Validate(string? name, AnafClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            return ValidateOptionsResult.Fail("An ANAF BaseUrl must be configured.");

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return ValidateOptionsResult.Fail($"The ANAF BaseUrl '{options.BaseUrl}' must be an absolute http(s) URL.");

        if (options.RequestsPerSecond < 0)
            return ValidateOptionsResult.Fail("ANAF RequestsPerSecond must be zero (disabled) or positive.");

        if (options.BatchSize < 1)
            return ValidateOptionsResult.Fail("ANAF BatchSize must be at least 1.");

        return ValidateOptionsResult.Success;
    }
}
