using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The request body for submitting an asynchronous run (<c>POST /v1/runs</c>). Unset (<c>null</c>)
///     properties are omitted from the request, so server-side defaults apply.
/// </summary>
public sealed record HermesAgentRunRequest
{
    /// <summary>
    ///     The run input — required by the server (<c>400</c> when missing). Either plain text (a string
    ///     converts implicitly) or a message list whose last item becomes the current-turn user message while the
    ///     earlier items become conversation history (unless <see cref="ConversationHistory" /> is supplied).
    ///     Unlike the chat-completions endpoint the runs endpoint performs no multimodal normalization —
    ///     message content is expected to be plain text.
    /// </summary>
    [JsonPropertyName("input")]
    public HermesAgentRunInput? Input { get; init; }

    /// <summary>
    ///     The model name to record on the run. Echoed in the status object; when it matches a server-configured
    ///     model-route alias that route's backend is used, and unknown values are simply ignored.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    ///     An ephemeral system prompt layered on top of the core Hermes prompt. When omitted, carried forward
    ///     from <see cref="PreviousResponseId" /> if a chain is used.
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    /// <summary>
    ///     The Hermes session/task id for the run (groups dashboard sessions). Defaults to the stored session id
    ///     from the <see cref="PreviousResponseId" /> chain, else the new run id. Unlike the
    ///     <c>X-Hermes-Session-Id</c> header path, this body field is not validated by the server for control
    ///     characters or path traversal — do not pass untrusted values.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>
    ///     A stored response id to chain conversation history from the Responses store. An unknown id is
    ///     silently ignored by this endpoint (no <c>404</c>, unlike <c>POST /v1/responses</c>).
    /// </summary>
    [JsonPropertyName("previous_response_id")]
    public string? PreviousResponseId { get; init; }

    /// <summary>
    ///     Explicit client-supplied conversation history; takes precedence over
    ///     <see cref="PreviousResponseId" />. Every entry must carry both a role and content (<c>400</c>
    ///     otherwise); content is coerced to a string by the server.
    /// </summary>
    [JsonPropertyName("conversation_history")]
    public IReadOnlyList<HermesAgentRunMessage>? ConversationHistory { get; init; }
}