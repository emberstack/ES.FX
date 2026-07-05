namespace ES.FX.NousResearch.HermesAgent.Abstractions;

/// <summary>
///     A typed client for the Nous Research Hermes Agent HTTP API, organized by resource area to mirror the
///     server's endpoint groups (OpenAI-compatible <c>/v1</c> surface plus the <c>/api</c> jobs and sessions
///     surfaces).
/// </summary>
public interface IHermesAgentClient
{
    /// <summary>Operations against the OpenAI-compatible <c>/v1/chat/completions</c> endpoint.</summary>
    IHermesAgentChatApi Chat { get; }

    /// <summary>Operations against the <c>/v1/responses</c> (Responses API) endpoints.</summary>
    IHermesAgentResponsesApi Responses { get; }

    /// <summary>Operations against the <c>/v1/runs</c> (structured asynchronous runs) endpoints.</summary>
    IHermesAgentRunsApi Runs { get; }

    /// <summary>Operations against the <c>/api/jobs</c> (scheduled jobs) endpoints.</summary>
    IHermesAgentJobsApi Jobs { get; }

    /// <summary>Operations against the <c>/api/sessions</c> (session management and session chat) endpoints.</summary>
    IHermesAgentSessionsApi Sessions { get; }

    /// <summary>Operations against the discovery and health endpoints (<c>/v1/models</c>, <c>/v1/capabilities</c>, <c>/v1/skills</c>, <c>/v1/toolsets</c>, <c>/v1/health</c>, <c>/health/detailed</c>).</summary>
    IHermesAgentServerApi Server { get; }
}
