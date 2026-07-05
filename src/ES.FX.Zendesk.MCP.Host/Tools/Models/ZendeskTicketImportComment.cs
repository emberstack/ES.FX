using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>A historical comment inside a <see cref="ZendeskTicketImport" />.</summary>
public sealed record ZendeskTicketImportComment
{
    [JsonPropertyName("author_id")] public long? AuthorId { get; init; }
    [JsonPropertyName("body")] public string? Body { get; init; }
    [JsonPropertyName("html_body")] public string? HtmlBody { get; init; }
    [JsonPropertyName("public")] public bool? Public { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}