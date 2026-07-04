using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ES.FX.Zendesk.Configuration;

/// <summary>
///     Validates <see cref="ZendeskClientOptions" /> so misconfiguration fails fast (when combined with
///     <c>ValidateOnStart()</c>) with a clear message.
/// </summary>
public sealed partial class ZendeskClientOptionsValidator : IValidateOptions<ZendeskClientOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ZendeskClientOptions options)
    {
        var failures = new List<string>();

        var hasBaseUrl = !string.IsNullOrWhiteSpace(options.BaseUrl);
        var hasSubdomain = !string.IsNullOrWhiteSpace(options.Subdomain);

        if (!hasSubdomain && !hasBaseUrl)
            failures.Add("A Zendesk Subdomain (or BaseUrl) must be configured.");

        // The subdomain is interpolated into https://{subdomain}.zendesk.com — reject anything that is not a
        // plain DNS label so a config typo cannot silently build a URL targeting a different host. When BaseUrl
        // is set it takes precedence and the subdomain is not used, so it is not validated.
        if (hasSubdomain && !hasBaseUrl && !SubdomainRegex().IsMatch(options.Subdomain))
            failures.Add(
                "Subdomain must be a single DNS label (letters, digits and inner hyphens only), " +
                $"but was '{options.Subdomain}'.");

        if (hasBaseUrl && (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl) ||
                           (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps)))
            failures.Add($"BaseUrl must be an absolute http(s) URL, but was '{options.BaseUrl}'.");

        if (string.IsNullOrWhiteSpace(options.OAuth.ClientId))
            failures.Add("OAuth:ClientId is required for Zendesk OAuth authentication.");

        if (string.IsNullOrWhiteSpace(options.OAuth.ClientSecret))
            failures.Add("OAuth:ClientSecret is required for Zendesk OAuth authentication.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    [GeneratedRegex("^[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?$")]
    private static partial Regex SubdomainRegex();
}