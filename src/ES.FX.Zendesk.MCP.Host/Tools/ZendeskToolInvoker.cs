using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     Invokes a Zendesk API-client call on behalf of an MCP tool and surfaces Zendesk failures back to the
///     calling agent.
/// </summary>
/// <remarks>
///     The MCP SDK catches any exception thrown by a tool and, unless it is an <see cref="McpException" />,
///     replaces it with an opaque <c>"An error occurred invoking '{tool}'."</c> result — discarding the HTTP
///     status code and the Zendesk error body carried by <see cref="ZendeskApiException" />. Routing tool calls
///     through here re-throws a <see cref="ZendeskApiException" /> as an <see cref="McpException" />, whose message
///     the SDK surfaces verbatim, so the agent can distinguish (for example) <c>404 Not Found</c> from
///     <c>403 Forbidden</c> from <c>422</c> and self-correct. Only the typed <see cref="ZendeskApiException" /> is
///     translated; other exceptions keep their default (generic) SDK handling.
/// </remarks>
internal static class ZendeskToolInvoker
{
    public static async Task<T> InvokeAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (ZendeskApiException exception)
        {
            throw new McpException(Describe(exception));
        }
    }

    private static string Describe(ZendeskApiException exception)
    {
        var message =
            $"The Zendesk API request failed with status {(int)exception.StatusCode} ({exception.StatusCode}).";
        return string.IsNullOrWhiteSpace(exception.ResponseBody)
            ? message
            : $"{message} Zendesk response: {exception.ResponseBody}";
    }
}