using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The request body for resolving a pending tool approval (<c>POST /v1/runs/{run_id}/approval</c>).
///     Unset (<c>null</c>) properties are omitted from the request, so server-side defaults apply.
/// </summary>
public sealed record HermesAgentRunApprovalRequest
{
    /// <summary>
    ///     The approval decision — required by the server. Matched case-insensitively; canonical values are
    ///     <c>once</c>, <c>session</c>, <c>always</c> and <c>deny</c>, and the aliases <c>approve</c>,
    ///     <c>approved</c> and <c>allow</c> map to <c>once</c>. Anything else returns <c>400</c> with code
    ///     <c>invalid_approval_choice</c>.
    /// </summary>
    [JsonPropertyName("choice")]
    public string? Choice { get; init; }

    /// <summary>Whether to resolve every queued approval for the run (server default <c>false</c>).</summary>
    [JsonPropertyName("all")]
    public bool? All { get; init; }

    /// <summary>Synonym of <see cref="All" />; the server ORs the two flags together.</summary>
    [JsonPropertyName("resolve_all")]
    public bool? ResolveAll { get; init; }
}
