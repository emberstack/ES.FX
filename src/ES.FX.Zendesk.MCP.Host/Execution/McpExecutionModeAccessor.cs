using ES.FX.Zendesk.MCP.Host.Configuration;
using Microsoft.Extensions.Options;

namespace ES.FX.Zendesk.MCP.Host.Execution;

/// <summary>
///     Default <see cref="IMcpExecutionModeAccessor" /> implementation. Resolves the effective mode from the
///     configured baseline and (optionally) a request header on the current <see cref="HttpContext" />.
/// </summary>
internal sealed class McpExecutionModeAccessor(
    IHttpContextAccessor httpContextAccessor,
    IOptionsMonitor<McpOptions> options) : IMcpExecutionModeAccessor
{
    /// <inheritdoc />
    public McpExecutionMode ConfiguredMode => options.CurrentValue.Execution.Mode;

    /// <inheritdoc />
    public McpExecutionMode EffectiveMode
    {
        get
        {
            var execution = options.CurrentValue.Execution;
            var headerValue = httpContextAccessor.HttpContext?.Request.Headers[execution.HeaderName].ToString();
            return McpExecutionModeResolver.Resolve(execution.Mode, headerValue, execution.AllowHeaderOverride);
        }
    }
}