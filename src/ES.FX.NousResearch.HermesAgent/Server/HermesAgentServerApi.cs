using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.NousResearch.HermesAgent.Server;

/// <summary>
///     Default <see cref="IHermesAgentServerApi" /> implementation over the shared Hermes Agent
///     <see cref="HttpClient" />.
/// </summary>
internal sealed class HermesAgentServerApi(HttpClient httpClient, ILogger<HermesAgentServerApi> logger)
    : HermesAgentResourceApi(httpClient, logger), IHermesAgentServerApi
{
    /// <inheritdoc />
    public Task<HermesAgentModelsResult> GetModelsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<HermesAgentModelsResult>("v1/models", "HermesAgent.Server.GetModels", cancellationToken);

    /// <inheritdoc />
    public Task<HermesAgentCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<HermesAgentCapabilities>("v1/capabilities", "HermesAgent.Server.GetCapabilities",
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<HermesAgentSkill>> GetSkillsAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<HermesAgentSkillsResult>("v1/skills", "HermesAgent.Server.GetSkills",
            cancellationToken).ConfigureAwait(false);
        return response.Data;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HermesAgentToolset>> GetToolsetsAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<HermesAgentToolsetsResult>("v1/toolsets", "HermesAgent.Server.GetToolsets",
            cancellationToken).ConfigureAwait(false);
        return response.Data;
    }

    /// <inheritdoc />
    public Task<HermesAgentHealth> GetHealthAsync(CancellationToken cancellationToken = default) =>
        GetAsync<HermesAgentHealth>("v1/health", "HermesAgent.Server.GetHealth", cancellationToken);

    // QUIRK: the detailed health endpoint lives at the root (`/health/detailed`), not under `/v1` — the
    // relative URI composes against the trailing-slash base address the same way either way.
    /// <inheritdoc />
    public Task<HermesAgentDetailedHealth> GetDetailedHealthAsync(CancellationToken cancellationToken = default) =>
        GetAsync<HermesAgentDetailedHealth>("health/detailed", "HermesAgent.Server.GetDetailedHealth",
            cancellationToken);
}
