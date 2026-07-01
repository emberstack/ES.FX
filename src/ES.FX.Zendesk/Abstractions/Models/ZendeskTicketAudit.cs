using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     An immutable record of an update to a ticket. Each audit bundles one or more <see cref="Events" />
///     (field changes, comments, notifications, ...).
/// </summary>
public sealed record ZendeskTicketAudit
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("ticket_id")] public long? TicketId { get; init; }
    [JsonPropertyName("author_id")] public long? AuthorId { get; init; }
    [JsonPropertyName("via")] public ZendeskVia? Via { get; init; }
    [JsonPropertyName("events")] public IReadOnlyList<ZendeskAuditEvent> Events { get; init; } = [];
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}