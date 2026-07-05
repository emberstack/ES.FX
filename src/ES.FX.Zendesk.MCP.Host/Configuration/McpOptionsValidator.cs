using Microsoft.Extensions.Options;

namespace ES.FX.Zendesk.MCP.Host.Configuration;

/// <summary>
///     Validates <see cref="McpOptions" /> so misconfiguration fails fast at startup (combined with
///     <c>ValidateOnStart()</c>) with a message naming the offending configuration key. Note the tool-area list
///     is validated separately (and earlier) by <c>ZendeskToolAreaGate.FromConfiguration</c>, which needs the
///     tool assembly to enumerate valid areas.
/// </summary>
public sealed class McpOptionsValidator : IValidateOptions<McpOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, McpOptions options)
    {
        var failures = new List<string>();

        if (options.Tools.MaxResponseChars < McpToolsOptions.MinimumMaxResponseChars)
            failures.Add(
                $"Mcp:Tools:MaxResponseChars must be at least {McpToolsOptions.MinimumMaxResponseChars}, " +
                $"but was {options.Tools.MaxResponseChars}.");

        foreach (var (toolName, maxResponseChars) in options.Tools.MaxResponseCharsByTool)
            if (maxResponseChars < McpToolsOptions.MinimumMaxResponseChars)
                failures.Add(
                    $"Mcp:Tools:MaxResponseCharsByTool:{toolName} must be at least " +
                    $"{McpToolsOptions.MinimumMaxResponseChars}, but was {maxResponseChars}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}