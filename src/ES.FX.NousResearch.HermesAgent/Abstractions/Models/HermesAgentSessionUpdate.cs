using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The updatable fields of a session (<c>PATCH /api/sessions/{session_id}</c>). The server accepts exactly
///     these two fields — anything else is rejected with <c>400 unsupported_session_field</c>. Unset
///     (<c>null</c>) properties are omitted from the request.
/// </summary>
public sealed record HermesAgentSessionUpdate
{
    /// <summary>
    ///     The new title (uniqueness and 100-character rules apply — <c>400 invalid_title</c> on failure).
    ///     <c>null</c> is omitted from the request (no change); to CLEAR a title send an empty or
    ///     whitespace-only string, which the server stores as no title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    ///     Marks the session ended with this reason. Applied only when non-empty. The FIRST end reason wins:
    ///     updating an already-ended session is a silent no-op (the response still shows the old value), and an
    ///     end reason cannot be cleared via this endpoint.
    /// </summary>
    [JsonPropertyName("end_reason")]
    public string? EndReason { get; init; }
}
