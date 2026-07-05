using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     Options for forking a session (<c>POST /api/sessions/{session_id}/fork</c>). All fields are optional;
///     unset (<c>null</c>) properties are omitted and server defaults apply.
/// </summary>
public sealed record HermesAgentSessionForkRequest
{
    /// <summary>
    ///     An explicit id for the fork. When omitted the server generates <c>api_{unix_ts}_{8 hex}</c>.
    ///     Unlike create, fork only rejects control characters — the path-unsafety and length checks are NOT
    ///     applied, so do not rely on the server to normalize the value. <c>409 session_exists</c> if taken.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    ///     The fork title (uniqueness and length rules apply). Defaults to the next title in the source's
    ///     lineage (falling back to <c>{source title} fork</c>). On title failure the fork and its copied
    ///     messages are LEFT IN PLACE while <c>400 invalid_title</c> is returned.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}
