using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>
///     A historical ticket for the admin-only import channel (<c>POST /api/v2/imports/tickets.json</c>). Unlike a
///     regular create, an import accepts a whole <see cref="Comments" /> conversation, historical timestamps, and
///     <see cref="SolvedAt" />; triggers do not fire for non-closed statuses and metrics/SLAs are not applied.
/// </summary>
public sealed record ZendeskTicketImport
{
    [JsonPropertyName("subject")] public string? Subject { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("requester_id")] public long? RequesterId { get; init; }
    [JsonPropertyName("assignee_id")] public long? AssigneeId { get; init; }

    /// <summary>The status — one of new, open, pending, hold, solved, closed.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("tags")] public IReadOnlyList<string>? Tags { get; init; }
    [JsonPropertyName("brand_id")] public long? BrandId { get; init; }
    [JsonPropertyName("custom_fields")] public IReadOnlyList<ZendeskCustomFieldWrite>? CustomFields { get; init; }
    [JsonPropertyName("comments")] public IReadOnlyList<ZendeskTicketImportComment>? Comments { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
    [JsonPropertyName("solved_at")] public DateTimeOffset? SolvedAt { get; init; }
}