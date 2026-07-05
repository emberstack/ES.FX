using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Chat;
using ES.FX.NousResearch.HermesAgent.Jobs;
using ES.FX.NousResearch.HermesAgent.Responses;
using ES.FX.NousResearch.HermesAgent.Runs;
using ES.FX.NousResearch.HermesAgent.Server;
using ES.FX.NousResearch.HermesAgent.Sessions;
using Microsoft.Extensions.Logging;

namespace ES.FX.NousResearch.HermesAgent;

/// <summary>
///     Default <see cref="IHermesAgentClient" /> implementation. Registered as a typed <see cref="HttpClient" />
///     whose base address targets the configured Hermes Agent API server and whose authorization header is
///     applied by <see cref="Authentication.HermesAgentAuthenticationDelegatingHandler" />.
/// </summary>
internal sealed class HermesAgentClient(HttpClient httpClient, ILoggerFactory loggerFactory) : IHermesAgentClient
{
    /// <inheritdoc />
    public IHermesAgentChatApi Chat { get; } =
        new HermesAgentChatApi(httpClient, loggerFactory.CreateLogger<HermesAgentChatApi>());

    /// <inheritdoc />
    public IHermesAgentResponsesApi Responses { get; } =
        new HermesAgentResponsesApi(httpClient, loggerFactory.CreateLogger<HermesAgentResponsesApi>());

    /// <inheritdoc />
    public IHermesAgentRunsApi Runs { get; } =
        new HermesAgentRunsApi(httpClient, loggerFactory.CreateLogger<HermesAgentRunsApi>());

    /// <inheritdoc />
    public IHermesAgentJobsApi Jobs { get; } =
        new HermesAgentJobsApi(httpClient, loggerFactory.CreateLogger<HermesAgentJobsApi>());

    /// <inheritdoc />
    public IHermesAgentSessionsApi Sessions { get; } =
        new HermesAgentSessionsApi(httpClient, loggerFactory.CreateLogger<HermesAgentSessionsApi>());

    /// <inheritdoc />
    public IHermesAgentServerApi Server { get; } =
        new HermesAgentServerApi(httpClient, loggerFactory.CreateLogger<HermesAgentServerApi>());
}
