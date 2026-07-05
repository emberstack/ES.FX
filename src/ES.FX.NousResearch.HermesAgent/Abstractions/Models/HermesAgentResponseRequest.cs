using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The request body of a Responses API call (<c>POST /v1/responses</c>). Unset (<c>null</c>) properties are
///     omitted from the request. The <c>stream</c> flag is deliberately not modeled — it is enforced by the
///     client method used (<c>CreateAsync</c> forces <c>false</c>, <c>StreamAsync</c> forces <c>true</c>).
/// </summary>
public sealed record HermesAgentResponseRequest
{
    /// <summary>
    ///     The input — a plain string (single user message) or a list of input messages. Required by the server
    ///     (<c>400</c> when missing or empty of visible content). A <see cref="string" /> converts implicitly.
    /// </summary>
    [JsonPropertyName("input")]
    public required HermesAgentResponseInput Input { get; init; }

    /// <summary>
    ///     The model name. Echoed back in the response; a value matching a configured server model route selects
    ///     that route's backend, unknown values are silently ignored.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    ///     The ephemeral system prompt layered on top of the core Hermes prompt. When omitted while chaining via
    ///     <see cref="PreviousResponseId" />, the chained response's instructions are carried forward.
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    /// <summary>
    ///     Whether to persist the response for <c>GET /v1/responses/{id}</c> and chaining. Server default:
    ///     <c>true</c>. The store keeps at most 100 responses (least-recently-used eviction).
    /// </summary>
    [JsonPropertyName("store")]
    public bool? Store { get; init; }

    /// <summary>
    ///     The id of a stored response to chain from (restores its history, session id and instructions). Unknown
    ///     ids yield a <c>404</c> unless <see cref="ConversationHistory" /> is also supplied. Mutually exclusive
    ///     with <see cref="Conversation" /> (<c>400</c> when both are set).
    /// </summary>
    [JsonPropertyName("previous_response_id")]
    public string? PreviousResponseId { get; init; }

    /// <summary>
    ///     A named conversation, resolved server-side to its latest response id. An unknown name starts a new
    ///     conversation (no error); on store, the name is re-pointed to the new response. Mutually exclusive with
    ///     <see cref="PreviousResponseId" />.
    /// </summary>
    [JsonPropertyName("conversation")]
    public string? Conversation { get; init; }

    /// <summary>
    ///     Explicit client-supplied history (a Hermes extension). Takes precedence over
    ///     <see cref="PreviousResponseId" />. Every item must carry both a role and content or the server rejects
    ///     the request with a <c>400</c>.
    /// </summary>
    [JsonPropertyName("conversation_history")]
    public IReadOnlyList<HermesAgentResponseInputMessage>? ConversationHistory { get; init; }

    /// <summary>
    ///     The truncation strategy. Only <c>auto</c> has an effect: the history is trimmed to the most recent 100
    ///     messages.
    /// </summary>
    [JsonPropertyName("truncation")]
    public string? Truncation { get; init; }
}