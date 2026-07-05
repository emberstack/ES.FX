using ES.FX.NousResearch.HermesAgent.Abstractions.Models;

namespace ES.FX.NousResearch.HermesAgent.Abstractions;

/// <summary>
///     Operations against the Hermes Agent discovery and health endpoints (<c>/v1/models</c>,
///     <c>/v1/capabilities</c>, <c>/v1/skills</c>, <c>/v1/toolsets</c>, <c>/v1/health</c>,
///     <c>/health/detailed</c>).
/// </summary>
public interface IHermesAgentServerApi
{
    /// <summary>
    ///     Lists the models advertised by the server (<c>GET /v1/models</c>). The first entry is the advertised
    ///     model name; one additional entry is returned per configured model-route alias (with the alias's
    ///     resolved model name as <see cref="HermesAgentModel.Root" /> and the advertised model as
    ///     <see cref="HermesAgentModel.Parent" />). <see cref="HermesAgentModel.Created" /> is the unix time of
    ///     the request, not a real creation time. No credentials are ever exposed.
    /// </summary>
    Task<HermesAgentModelsResult> GetModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the server's capability document (<c>GET /v1/capabilities</c>): platform, advertised model,
    ///     auth requirements, runtime mode, feature flags and the endpoint catalog. Requires authentication, so a
    ///     successful call also proves the configured API key is valid.
    /// </summary>
    Task<HermesAgentCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the skills known to the server's skills hub (<c>GET /v1/skills</c>; unwraps the
    ///     <c>{"object":"list","data":[...]}</c> envelope). Entries are open metadata objects — the server
    ///     guarantees at least <c>name</c>, <c>description</c> and <c>category</c>; any additional metadata keys
    ///     are not surfaced by this client. Whether disabled skills are excluded is not guaranteed by the server.
    /// </summary>
    Task<IReadOnlyList<HermesAgentSkill>> GetSkillsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the server's toolsets (<c>GET /v1/toolsets</c>; unwraps the <c>{"object":"list","data":[...]}</c>
    ///     envelope). Each toolset's <see cref="HermesAgentToolset.Tools" /> is a sorted, de-duplicated list of
    ///     concrete tool names; a toolset that fails to resolve server-side still appears, with an empty list.
    /// </summary>
    Task<IReadOnlyList<HermesAgentToolset>> GetToolsetsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the basic liveness document (<c>GET /v1/health</c>, an alias of <c>GET /health</c>). This is
    ///     the only endpoint that works without authentication — a success proves reachability but NOT that the
    ///     configured API key is valid (use <see cref="GetCapabilitiesAsync" /> for that).
    /// </summary>
    Task<HermesAgentHealth> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the detailed runtime health document (<c>GET /health/detailed</c> — note: NOT under
    ///     <c>/v1</c>). Requires authentication. When the server's runtime status file is missing,
    ///     <see cref="HermesAgentDetailedHealth.GatewayState" /> is <c>null</c> and
    ///     <see cref="HermesAgentDetailedHealth.Platforms" /> is empty;
    ///     <see cref="HermesAgentDetailedHealth.Pid" /> is always the live process id.
    /// </summary>
    Task<HermesAgentDetailedHealth> GetDetailedHealthAsync(CancellationToken cancellationToken = default);
}
