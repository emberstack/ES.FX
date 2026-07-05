using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.NousResearch.HermesAgent.Jobs;

/// <summary>
///     Default <see cref="IHermesAgentJobsApi" /> implementation over the shared Hermes Agent
///     <see cref="HttpClient" />. The jobs surface lives under <c>/api</c> (not <c>/v1</c>), sends write
///     models as flat bodies (no request envelope) and wraps every response in a <c>job</c>/<c>jobs</c>
///     envelope.
/// </summary>
internal sealed class HermesAgentJobsApi(HttpClient httpClient, ILogger<HermesAgentJobsApi> logger)
    : HermesAgentResourceApi(httpClient, logger), IHermesAgentJobsApi
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<HermesAgentJob>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<HermesAgentJobsResult>("api/jobs", "HermesAgent.Jobs.List",
            cancellationToken).ConfigureAwait(false);
        return result.Jobs;
    }

    /// <inheritdoc />
    public async Task<HermesAgentJob> CreateAsync(HermesAgentJobWrite job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var response = await PostAsync<HermesAgentJobResponse>("api/jobs", job, "HermesAgent.Jobs.Create",
            cancellationToken).ConfigureAwait(false);
        return response.Job ?? throw new InvalidOperationException("Hermes Agent returned no created job.");
    }

    /// <inheritdoc />
    public async Task<HermesAgentJob> GetByIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        var response = await GetAsync<HermesAgentJobResponse>(JobUri(jobId), "HermesAgent.Jobs.Get",
            cancellationToken).ConfigureAwait(false);
        return response.Job ??
               throw new InvalidOperationException($"Hermes Agent returned no job for id '{jobId}'.");
    }

    /// <inheritdoc />
    public async Task<HermesAgentJob> UpdateAsync(string jobId, HermesAgentJobWrite job,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentNullException.ThrowIfNull(job);

        var response = await PatchAsync<HermesAgentJobResponse>(JobUri(jobId), job, "HermesAgent.Jobs.Update",
            cancellationToken).ConfigureAwait(false);
        return response.Job ?? throw new InvalidOperationException("Hermes Agent returned no updated job.");
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string jobId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        // QUIRK: the server acknowledges with `200 {"ok": true}` (never 204), so the typed overload is used to
        // keep the shared deserialize-or-throw flow; failures already surface via the response guard.
        _ = await DeleteAsync<HermesAgentJobDeleteResponse>(JobUri(jobId), "HermesAgent.Jobs.Delete",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<HermesAgentJob> PauseAsync(string jobId, CancellationToken cancellationToken = default) =>
        LifecycleAsync(jobId, "pause", "HermesAgent.Jobs.Pause", cancellationToken);

    /// <inheritdoc />
    public Task<HermesAgentJob> ResumeAsync(string jobId, CancellationToken cancellationToken = default) =>
        LifecycleAsync(jobId, "resume", "HermesAgent.Jobs.Resume", cancellationToken);

    /// <inheritdoc />
    public Task<HermesAgentJob> TriggerAsync(string jobId, CancellationToken cancellationToken = default) =>
        LifecycleAsync(jobId, "run", "HermesAgent.Jobs.Trigger", cancellationToken);

    /// <summary>
    ///     Sends a body-less lifecycle <c>POST</c> (<c>pause</c>/<c>resume</c>/<c>run</c>) and unwraps the
    ///     returned <c>{ "job": { ... } }</c> envelope.
    /// </summary>
    private async Task<HermesAgentJob> LifecycleAsync(string jobId, string action, string operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        var response = await PostAsync<HermesAgentJobResponse>($"{JobUri(jobId)}/{action}", operation,
            cancellationToken).ConfigureAwait(false);
        return response.Job ??
               throw new InvalidOperationException($"Hermes Agent returned no job for '{operation}'.");
    }

    // The server only accepts 12-hex ids, but escaping keeps a malformed caller-supplied id from ever mutating
    // the request path (it reaches the server verbatim and fails its id validation instead).
    private static string JobUri(string jobId) => $"api/jobs/{Uri.EscapeDataString(jobId)}";
}
