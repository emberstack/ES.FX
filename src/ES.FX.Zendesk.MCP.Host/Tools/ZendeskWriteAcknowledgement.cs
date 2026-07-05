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

    /// <summary>
    ///     The id of the affected record when the operation targeted exactly one — structured, so the agent does
    ///     not have to parse it back out of <see cref="Description" /> prose. Omitted when not applicable.
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Id { get; init; }

    /// <summary>
    ///     The ids of the affected records when the operation targeted several. Omitted when not applicable.
    /// </summary>
    [JsonPropertyName("ids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<long>? Ids { get; init; }
}