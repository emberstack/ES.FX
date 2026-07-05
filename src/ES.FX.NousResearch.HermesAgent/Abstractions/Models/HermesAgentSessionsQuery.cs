namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     Filters for listing sessions (<c>GET /api/sessions</c>). Values are sent as query-string parameters;
///     <c>null</c> values are omitted and server defaults apply. Invalid values never fail server-side — they
///     silently coerce to the defaults, and out-of-range values are clamped.
/// </summary>
public sealed record HermesAgentSessionsQuery
{
    /// <summary>The page size (<c>limit</c>). Server default <c>50</c>; clamped to a maximum of <c>200</c>.</summary>
    public int? Limit { get; init; }

    /// <summary>The rows to skip (<c>offset</c>). Server default <c>0</c>; clamped to a maximum of <c>1000000</c>.</summary>
    public int? Offset { get; init; }

    /// <summary>
    ///     An exact-match filter on the session <c>source</c> (e.g. <c>api_server</c>, <c>cli</c>,
    ///     <c>telegram</c>, <c>cron</c>). An empty string is treated as absent.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    ///     When <c>true</c>, returns raw rows including subagent runs, compression continuations and delegate
    ///     children (<c>include_children</c>). When <c>false</c> (server default), children are hidden and
    ///     compression roots are projected forward to their live tip, gaining
    ///     <see cref="HermesAgentSession.LineageRootId" />.
    /// </summary>
    public bool? IncludeChildren { get; init; }
}
