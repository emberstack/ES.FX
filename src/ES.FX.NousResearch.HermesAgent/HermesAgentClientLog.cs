using Microsoft.Extensions.Logging;

namespace ES.FX.NousResearch.HermesAgent;

/// <summary>
///     Source-generated log messages for the Hermes Agent client (allocation-free and level-guarded). Response
///     bodies and the API key are deliberately never logged — bodies can contain conversation content; the
///     (truncated) error body remains available on <see cref="HermesAgentApiException.ResponseBody" />.
/// </summary>
internal static partial class HermesAgentClientLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "HermesAgent {Operation} succeeded")]
    public static partial void OperationSucceeded(ILogger logger, string operation);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "HermesAgent {Operation} failed with status {StatusCode}")]
    public static partial void OperationFailedWithStatus(ILogger logger, Exception exception, string operation,
        int statusCode);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "HermesAgent {Operation} failed")]
    public static partial void OperationFailed(ILogger logger, Exception exception, string operation);
}
