using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>uploads</c> resource — attachment uploads consumed by ticket comments.
/// </summary>
public interface IZendeskUploadsApi
{
    /// <summary>
    ///     Uploads a file (<c>POST /api/v2/uploads.json?filename=</c>; raw binary body, 50 MB limit). Pass the
    ///     returned token as <paramref name="token" /> on subsequent uploads to bundle multiple files, then attach
    ///     it via <c>ZendeskTicketCommentWrite.Uploads</c>. Tokens are single-use and expire after 60 minutes;
    ///     until consumed, the file is reachable by any authenticated user via its <c>content_url</c>.
    /// </summary>
    /// <param name="fileName">The file name (the extension must match the actual content).</param>
    /// <param name="content">The file bytes.</param>
    /// <param name="contentType">The file's real MIME type (a wrong type causes undesired behavior).</param>
    /// <param name="token">An existing upload token to append this file to, for multi-file bundles.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskUpload> UploadAsync(string fileName, ReadOnlyMemory<byte> content, string contentType,
        string? token = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes an unconsumed upload by its token (<c>DELETE /api/v2/uploads/{token}.json</c>).</summary>
    Task DeleteAsync(string token, CancellationToken cancellationToken = default);
}