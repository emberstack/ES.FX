using System.ComponentModel;
using System.Text.Json.Serialization;
using ES.FX.Zendesk.Attachments;
using ES.FX.Zendesk.Support;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk attachments. Namespaced <c>attachments_*</c>. Attachment metadata is read via the
///     generated Support client; the payload behind <c>content_url</c> is downloaded through the curated
///     <see cref="ZendeskAttachmentContentFetcher" /> (the generated client cannot express the content download).
///     <c>attachments_get</c> is exempt from the generic response-size guard by design — its own
///     <c>maxBytes</c> byte cap is the budget (see <c>McpToolsOptions.MaxResponseChars</c>).
/// </summary>
[McpServerToolType]
public sealed class ZendeskAttachmentTools(
    ZendeskSupportApiClient zendesk,
    ZendeskAttachmentContentFetcher contentFetcher)
{
    /// <summary>The default per-call byte cap (32 KiB).</summary>
    private const int DefaultMaxBytes = 32 * 1024;

    /// <summary>
    ///     The hard per-call byte cap (64 KiB): base64 of 64 KiB is ~87k characters (~22k tokens), just under the
    ///     25k-token client response cap. Larger payloads are paged with <c>offset</c>, never in one call.
    /// </summary>
    private const int MaxBytesPerCall = 64 * 1024;

    /// <summary>Downloads an attachment's content — byte-capped, resumable via <c>offset</c>.</summary>
    [McpServerTool(Name = "attachments_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Download a ticket attachment's content by id (attachments appear on ticket comments). Text/JSON/CSV/XML " +
        "returns decoded text ('encoding':'utf-8'); other binary returns base64 ('encoding':'base64'). At most " +
        "maxBytes raw bytes per call. 'size'=full payload size, 'returned_bytes'=what this call returned; " +
        "'truncated':true means more remains — continue with offset = previous offset + returned_bytes ('note' " +
        "gives the exact re-call). For encoding:'base64', each call returns base64 of only THIS call's bytes — to " +
        "reassemble, base64-decode each call separately and concatenate the raw bytes, never the base64 strings. " +
        "Capped UTF-8 text ends on a character boundary so chained continuations decode cleanly; any hand-picked " +
        "offset>0 (and any offset into non-UTF-8 text) may start mid-character.")]
    public Task<ZendeskAttachmentContentResult> Read(
        [Description(
            "Numeric attachment id. Not directly listable — get it from a ticket comment's attachments[].id (list " +
            "the ticket's comments first).")]
        long id,
        CancellationToken cancellationToken,
        [Description(
            "Raw-byte cap for THIS call. Default 32768 (32 KiB); hard cap 65536 (64 KiB — base64 of 64 KiB is " +
            "~22k tokens, near the client response cap). Larger values rejected — page bigger payloads via 'offset'.")]
        int maxBytes = DefaultMaxBytes,
        [Description(
            "Raw payload bytes to skip before the cap applies (default 0). To continue a truncated download pass " +
            "previous offset + returned_bytes — guaranteed clean. Any other hand-picked offset>0 may land " +
            "mid-character (a leading partial character decodes to a replacement char).")]
        long offset = 0)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            if (maxBytes is <= 0 or > MaxBytesPerCall)
                throw new McpException(
                    $"Invalid maxBytes value '{maxBytes}'. Pass a byte cap between 1 and {MaxBytesPerCall} " +
                    $"(64 KiB — base64 of {MaxBytesPerCall} bytes is roughly 22k tokens, the per-call ceiling); " +
                    "page through larger payloads by re-calling with the offset parameter.");
            if (offset < 0)
                throw new McpException(
                    $"Invalid offset value '{offset}'. Pass 0 to start at the beginning, or the previous call's " +
                    "offset + returned_bytes to continue a truncated download.");

            var response = await zendesk.Api.V2.Attachments[id].GetAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var attachment = response?.Attachment
                             ?? throw new InvalidOperationException($"Zendesk attachment '{id}' was not found.");

            if (string.IsNullOrWhiteSpace(attachment.ContentUrl))
                throw new InvalidOperationException($"Zendesk attachment '{id}' has no downloadable content URL.");

            var content = await contentFetcher.DownloadAsync(attachment.ContentUrl, attachment.ContentType,
                maxBytes, offset, cancellationToken).ConfigureAwait(false);

            return new ZendeskAttachmentContentResult
            {
                Id = attachment.Id ?? id,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                Size = attachment.Size,
                ReturnedBytes = content.ReturnedBytes,
                Offset = offset,
                Truncated = content.Truncated,
                Note = content.Truncated
                    ? $"content truncated at the maxBytes:{maxBytes} cap — re-call with " +
                      $"offset:{offset + content.ReturnedBytes} (this call's offset + returned_bytes) to continue" +
                      (string.Equals(content.Encoding, "base64", StringComparison.OrdinalIgnoreCase)
                          ? "; base64-decode each call separately and concatenate the raw bytes, not the base64 strings"
                          : string.Empty)
                    : null,
                Encoding = content.Encoding,
                Content = content.Content
            };
        });
}

/// <summary>
///     The downloaded content of a Zendesk attachment: the attachment's metadata, the ranged-download bookkeeping
///     (<see cref="Size" /> vs. <see cref="ReturnedBytes" /> at <see cref="Offset" />), and the payload —
///     metadata first, the payload last. <see cref="Encoding" /> is <c>utf-8</c> when the attachment is text
///     (its <see cref="Content" /> is the decoded text) or <c>base64</c> for binary (its <see cref="Content" />
///     is base64). <see cref="Truncated" /> is <c>true</c> when the payload exceeded the per-call byte cap; the
///     <see cref="Note" /> then names the exact continuation re-call.
/// </summary>
public sealed record ZendeskAttachmentContentResult
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("file_name")] public string? FileName { get; init; }
    [JsonPropertyName("content_type")] public string? ContentType { get; init; }

    /// <summary>The FULL payload size Zendesk reports — not the number of bytes this call returned.</summary>
    [JsonPropertyName("size")]
    public long? Size { get; init; }

    /// <summary>
    ///     The raw payload bytes this call returned (after <see cref="Offset" />). For capped UTF-8 text this can
    ///     be slightly under the cap — the cut moves back to a character boundary.
    /// </summary>
    [JsonPropertyName("returned_bytes")]
    public int ReturnedBytes { get; init; }

    /// <summary>The raw-byte offset this call started at (echoed from the request).</summary>
    [JsonPropertyName("offset")]
    public long Offset { get; init; }

    [JsonPropertyName("truncated")] public bool Truncated { get; init; }

    /// <summary>The continuation recipe (present only when <see cref="Truncated" /> is <c>true</c>).</summary>
    [JsonPropertyName("note")]
    public string? Note { get; init; }

    [JsonPropertyName("encoding")] public string Encoding { get; init; } = "utf-8";
    [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;
}