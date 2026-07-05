namespace ES.FX.NousResearch.HermesAgent.Abstractions;

// The Hermes Agent server's vocabularies are server-defined and can grow; the client deliberately keeps
// string-typed members/parameters (an enum would make deserialization throw on a new server value) and ships
// the well-known values as constants instead, so consumers get discoverability without runtime fragility.

/// <summary>The well-known values of a run's <c>status</c>.</summary>
public static class HermesAgentRunStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string WaitingForApproval = "waiting_for_approval";
    public const string Stopping = "stopping";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

/// <summary>The well-known values of a response's <c>status</c> (Responses API).</summary>
public static class HermesAgentResponseStatuses
{
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Incomplete = "incomplete";
}

/// <summary>The well-known values of a scheduled job's <c>state</c>.</summary>
public static class HermesAgentJobStates
{
    public const string Scheduled = "scheduled";
    public const string Paused = "paused";
    public const string Completed = "completed";
    public const string Error = "error";
}

/// <summary>The well-known values of a job schedule's <c>kind</c>.</summary>
public static class HermesAgentScheduleKinds
{
    public const string Once = "once";
    public const string Interval = "interval";
    public const string Cron = "cron";
}

/// <summary>The well-known values of a job's last-run <c>status</c>.</summary>
public static class HermesAgentJobLastRunStatuses
{
    public const string Ok = "ok";
    public const string Error = "error";
}

/// <summary>The well-known values of a job's <c>deliver</c> mode.</summary>
public static class HermesAgentDeliverModes
{
    public const string Local = "local";
}

/// <summary>
///     The well-known values of a chat completion choice's <c>finish_reason</c>. <c>error</c> is a Hermes
///     extension (not part of the OpenAI vocabulary).
/// </summary>
public static class HermesAgentChatFinishReasons
{
    public const string Stop = "stop";
    public const string Length = "length";
    public const string Error = "error";
}
