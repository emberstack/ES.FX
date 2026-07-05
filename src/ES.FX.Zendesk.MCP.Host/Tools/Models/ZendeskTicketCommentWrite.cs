using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>A comment payload on a ticket create/update.</summary>
public sealed record ZendeskTicketCommentWrite
{
    /// <summary>The plain-text body (use exactly one of <see cref="Body" /> / <see cref="HtmlBody" />).</summary>
    [Description("Plain-text comment. Provide exactly one of body or html_body.")]
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary>The HTML body (use exactly one of <see cref="Body" /> / <see cref="HtmlBody" />).</summary>
    [Description("HTML comment. Provide exactly one of body or html_body.")]
    [JsonPropertyName("html_body")]
    public string? HtmlBody { get; init; }

    /// <summary><c>true</c> for a public reply, <c>false</c> for an internal note. Zendesk defaults to public.</summary>
    [Description(
        "true = public reply (visible to the requester); false = internal note (agents only).")]
    [JsonPropertyName("public")]
    public bool? Public { get; init; }

    /// <summary>The comment author; defaults to the authenticated user.</summary>
    [Description("Optional author user id; defaults to the authenticated API user.")]
    [JsonPropertyName("author_id")]
    public long? AuthorId { get; init; }

    /// <summary>Upload tokens (from <c>IZendeskUploadsApi.UploadAsync</c>) attaching files to the comment.</summary>
    [Description(
        "Array of upload tokens from uploads_create, to attach files to this comment.")]
    [JsonPropertyName("uploads")]
    public IReadOnlyList<string>? Uploads { get; init; }
}