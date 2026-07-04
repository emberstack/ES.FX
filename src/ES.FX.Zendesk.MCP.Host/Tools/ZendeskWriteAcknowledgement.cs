using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     The result a write tool returns for Zendesk operations whose API response carries no body (for example
///     restore, bulk-recover, or delete endpoints returning <c>204 No Content</c>): the operation was performed.
/// </summary>
public sealed record ZendeskWriteAcknowledgement
{
    /// <summary>Always <c>completed</c>.</summary>
    [JsonPropertyName("status")]
    public string Status => "completed";

    /// <summary>A human-readable statement of the change that was made.</summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }
}
