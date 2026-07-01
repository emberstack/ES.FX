using Microsoft.Extensions.Options;

namespace ES.FX.Zendesk.Configuration;

/// <summary>
///     Validates <see cref="ZendeskClientOptions" /> so misconfiguration fails fast (when combined with
///     <c>ValidateOnStart()</c>) with a clear message.
/// </summary>
public sealed class ZendeskClientOptionsValidator : IValidateOptions<ZendeskClientOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ZendeskClientOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Subdomain) && string.IsNullOrWhiteSpace(options.BaseUrl))
            failures.Add("A Zendesk Subdomain (or BaseUrl) must be configured.");

        if (string.IsNullOrWhiteSpace(options.OAuth.ClientId))
            failures.Add("OAuth:ClientId is required for Zendesk OAuth authentication.");

        if (string.IsNullOrWhiteSpace(options.OAuth.ClientSecret))
            failures.Add("OAuth:ClientSecret is required for Zendesk OAuth authentication.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}