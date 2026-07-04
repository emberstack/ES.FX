using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     The result of uploading a file (<c>POST /api/v2/uploads.json</c>). Attach the <see cref="Token" /> to a
///     ticket comment via <c>ZendeskTicketCommentWrite.Uploads</c>. Tokens are valid for 60 minutes and are
///     single-use; until consumed, the file is reachable by any authenticated user via its <c>content_url</c>.
/// </summary>
public sealed record ZendeskUpload
{
    /// <summary>The upload token — pass it on subsequent uploads to chain multiple files, then attach it to a comment.</summary>
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    /// <summary>The attachment created by THIS request.</summary>
    [JsonPropertyName("attachment")]
    public ZendeskAttachment? Attachment { get; init; }

    /// <summary>All attachments accumulated on the token (multi-file chaining).</summary>
    [JsonPropertyName("attachments")]
    public IReadOnlyList<ZendeskAttachment>? Attachments { get; init; }
}

/// <summary>The <c>{ "upload": {...} }</c> envelope.</summary>
public sealed record ZendeskUploadResponse
{
    [JsonPropertyName("upload")] public ZendeskUpload? Upload { get; init; }
}