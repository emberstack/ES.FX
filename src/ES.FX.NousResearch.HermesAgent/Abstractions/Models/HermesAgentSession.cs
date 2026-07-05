using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A Hermes Agent session — the client-safe projection returned by the <c>/api/sessions</c> endpoints.
///     Timestamps are Unix epoch seconds. The raw <c>system_prompt</c> / <c>model_config</c> values are never
///     exposed by the server; only <see cref="HasSystemPrompt" /> / <see cref="HasModelConfig" /> are.
/// </summary>
public sealed record HermesAgentSession
{
    /// <summary>The session identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    ///     The originating channel — <c>api_server</c> for sessions created via this API; other values include
    ///     <c>cli</c>, <c>telegram</c> and <c>cron</c>.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>The platform user id (<c>null</c> for API-created sessions).</summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>The model name snapshot recorded on the session.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>The session title (unique across all sessions; at most 100 characters).</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>When the session started (Unix epoch seconds).</summary>
    [JsonPropertyName("started_at")]
    public double StartedAt { get; init; }

    /// <summary>When the session ended (Unix epoch seconds), if it has ended.</summary>
    [JsonPropertyName("ended_at")]
    public double? EndedAt { get; init; }

    /// <summary>
    ///     Why the session ended, if it has ended. Free-form; notable values written by this API are
    ///     <c>branched</c> (the source of a fork) and anything set via <c>PATCH</c>.
    /// </summary>
    [JsonPropertyName("end_reason")]
    public string? EndReason { get; init; }

    /// <summary>The number of messages in the session.</summary>
    [JsonPropertyName("message_count")]
    public int MessageCount { get; init; }

    /// <summary>The number of tool calls made in the session.</summary>
    [JsonPropertyName("tool_call_count")]
    public int ToolCallCount { get; init; }

    /// <summary>The cumulative input tokens consumed by the session.</summary>
    [JsonPropertyName("input_tokens")]
    public long InputTokens { get; init; }

    /// <summary>The cumulative output tokens produced by the session.</summary>
    [JsonPropertyName("output_tokens")]
    public long OutputTokens { get; init; }

    /// <summary>The cumulative cache-read tokens consumed by the session.</summary>
    [JsonPropertyName("cache_read_tokens")]
    public long CacheReadTokens { get; init; }

    /// <summary>The cumulative cache-write tokens consumed by the session.</summary>
    [JsonPropertyName("cache_write_tokens")]
    public long CacheWriteTokens { get; init; }

    /// <summary>The cumulative reasoning tokens produced by the session.</summary>
    [JsonPropertyName("reasoning_tokens")]
    public long ReasoningTokens { get; init; }

    /// <summary>The estimated session cost in USD, if tracked.</summary>
    [JsonPropertyName("estimated_cost_usd")]
    public double? EstimatedCostUsd { get; init; }

    /// <summary>The actual session cost in USD, if tracked.</summary>
    [JsonPropertyName("actual_cost_usd")]
    public double? ActualCostUsd { get; init; }

    /// <summary>The number of model API calls made by the session.</summary>
    [JsonPropertyName("api_call_count")]
    public int ApiCallCount { get; init; }

    /// <summary>The parent session id for lineage (fork/branch/compression/subagent), if any.</summary>
    [JsonPropertyName("parent_session_id")]
    public string? ParentSessionId { get; init; }

    /// <summary>
    ///     The effective last activity (Unix epoch seconds). Populated on LIST responses only — absent
    ///     (<c>null</c>) on get/create/update/fork responses.
    /// </summary>
    [JsonPropertyName("last_active")]
    public double? LastActive { get; init; }

    /// <summary>
    ///     A preview of the first user message (at most 60 characters, <c>...</c>-suffixed when truncated).
    ///     Populated on LIST responses only.
    /// </summary>
    [JsonPropertyName("preview")]
    public string? Preview { get; init; }

    /// <summary>
    ///     The compression-lineage root session id, present on LIST responses only and only when the row is a
    ///     compression root projected forward to its live tip (the entry then carries the tip's id/title/counters
    ///     but keeps the root's <see cref="StartedAt" />).
    /// </summary>
    [JsonPropertyName("_lineage_root_id")]
    public string? LineageRootId { get; init; }

    /// <summary>Whether the session has a stored system prompt (the prompt itself is never exposed).</summary>
    [JsonPropertyName("has_system_prompt")]
    public bool HasSystemPrompt { get; init; }

    /// <summary>Whether the session has a stored model configuration (the configuration itself is never exposed).</summary>
    [JsonPropertyName("has_model_config")]
    public bool HasModelConfig { get; init; }
}