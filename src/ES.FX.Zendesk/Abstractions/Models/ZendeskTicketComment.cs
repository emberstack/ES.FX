using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A comment on a Zendesk ticket (the conversation thread). <see cref="Public" /> distinguishes an
///     agent/end-user visible reply (<c>true</c>) from an internal note (<c>false</c>).
/// </summary>
public sealed record ZendeskTicketComment
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("author_id")] public long? AuthorId { get; init; }

    /// <summary>The comment body as plain text.</summary>
    [JsonPropertyName("plain_body")]
    public string? PlainBody { get; init; }

    /// <summary>The comment body (may contain markup). Prefer <see cref="PlainBody" /> for plain text.</summary>
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary><c>true</c> for a public reply; <c>false</c> for an internal note.</summary>
    [JsonPropertyName("public")]
    public bool Public { get; init; }

    [JsonPropertyName("attachments")] public IReadOnlyList<ZendeskAttachment>? Attachments { get; init; }
    [JsonPropertyName("via")] public ZendeskVia? Via { get; init; }
    [JsonPropertyName("audit_id")] public long? AuditId { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}