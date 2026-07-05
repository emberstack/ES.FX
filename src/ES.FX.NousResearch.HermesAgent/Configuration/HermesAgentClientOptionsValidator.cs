using Microsoft.Extensions.Options;

namespace ES.FX.NousResearch.HermesAgent.Configuration;

/// <summary>
///     Validates <see cref="HermesAgentClientOptions" /> so misconfiguration fails fast (when combined with
///     <c>ValidateOnStart()</c>) with a clear message.
/// </summary>
public sealed class HermesAgentClientOptionsValidator : IValidateOptions<HermesAgentClientOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, HermesAgentClientOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            failures.Add("A Hermes Agent BaseUrl must be configured (e.g. 'http://localhost:8642').");
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl) ||
                 (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
            failures.Add($"BaseUrl must be an absolute http(s) URL, but was '{options.BaseUrl}'.");

        if (string.IsNullOrWhiteSpace(options.ApiKey))
            failures.Add("ApiKey is required for Hermes Agent bearer authentication.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}