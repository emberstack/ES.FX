using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk;

/// <summary>
///     Source-generated log messages for the Zendesk client (allocation-free and level-guarded). Response bodies
///     are deliberately never logged — they can contain requester PII; they remain available on
///     <see cref="ZendeskApiException.ResponseBody" />.
/// </summary>
internal static partial class ZendeskClientLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Zendesk {Operation} succeeded")]
    public static partial void OperationSucceeded(ILogger logger, string operation);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Zendesk {Operation} failed with status {StatusCode}")]
    public static partial void OperationFailedWithStatus(ILogger logger, Exception exception, string operation,
        int statusCode);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Zendesk {Operation} failed")]
    public static partial void OperationFailed(ILogger logger, Exception exception, string operation);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "Acquired Zendesk OAuth access token (expires in {ExpiresInSeconds}s)")]
    public static partial void TokenAcquired(ILogger logger, int expiresInSeconds);
}