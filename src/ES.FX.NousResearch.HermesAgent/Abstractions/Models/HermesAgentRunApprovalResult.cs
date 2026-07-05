using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The result of resolving a pending tool approval (<c>POST /v1/runs/{run_id}/approval</c>). The server
///     also pushes a matching <c>approval.responded</c> event
///     (<see cref="HermesAgentRunApprovalRespondedEvent" />) and flips the run status back to <c>running</c>.
/// </summary>
public sealed record HermesAgentRunApprovalResult
{
    /// <summary>The object discriminator (<c>hermes.run.approval_response</c>).</summary>
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    /// <summary>The identifier of the run the approval belongs to.</summary>
    [JsonPropertyName("run_id")]
    public string? RunId { get; init; }

    /// <summary>The canonical resolved choice (<c>once</c>, <c>session</c>, <c>always</c> or <c>deny</c>).</summary>
    [JsonPropertyName("choice")]
    public string? Choice { get; init; }

    /// <summary>The number of queued approvals that were resolved by the request.</summary>
    [JsonPropertyName("resolved")]
    public int? Resolved { get; init; }
}