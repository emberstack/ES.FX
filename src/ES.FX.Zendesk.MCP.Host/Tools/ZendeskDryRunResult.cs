using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     The result a write tool returns when the effective execution mode is
///     <see cref="Execution.McpExecutionMode.DryRun" />: the request was accepted and validated, but no change
///     was made. The payload states this explicitly so the calling agent is never led to believe the write
///     happened.
/// </summary>
public sealed record ZendeskDryRunResult
{
    /// <summary>Always <c>dry_run</c>.</summary>
    [JsonPropertyName("status")]
    public string Status => "dry_run";

    /// <summary>Always <c>false</c> — no change was made.</summary>
    [JsonPropertyName("executed")]
    public bool Executed => false;

    /// <summary>A human-readable statement of the change that would have been made.</summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>The request payload that would have been sent to Zendesk, echoed for inspection.</summary>
    [JsonPropertyName("request")]
    public object? Request { get; init; }
}
