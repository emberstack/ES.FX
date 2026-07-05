using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The writable fields for creating an empty session (<c>POST /api/sessions</c>). All fields are optional;
///     unset (<c>null</c>) properties are omitted from the request and server defaults apply.
/// </summary>
public sealed record HermesAgentSessionWrite
{
    /// <summary>
    ///     An explicit session id. When omitted the server generates <c>api_{unix_ts}_{8 hex}</c>. Must be
    ///     non-empty, contain no control characters, path separators, <c>..</c> or drive prefixes, and be at
    ///     most 256 characters (<c>400 invalid_session_id</c> otherwise; <c>409 session_exists</c> if taken).
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The model name to record on the session. Defaults to the server's advertised model name.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    ///     A session-scoped system prompt. Never exposed by reads — see
    ///     <see cref="HermesAgentSession.HasSystemPrompt" />.
    /// </summary>
    [JsonPropertyName("system_prompt")]
    public string? SystemPrompt { get; init; }

    /// <summary>
    ///     The session title. Must be unique across ALL sessions and at most 100 characters after whitespace
    ///     collapsing; on failure the just-created session is rolled back and <c>400 invalid_title</c> returned.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}