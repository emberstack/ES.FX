using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against Zendesk <c>attachments</c>. The attachment <c>content_url</c> found on comments requires
///     authenticated download for private tickets, which an agent cannot do itself — this fetches the bytes
///     server-side with the configured credentials.
/// </summary>
public interface IZendeskAttachmentsApi
{
    /// <summary>
    ///     Downloads an attachment's content (<c>GET /api/v2/attachments/{id}.json</c> then its <c>content_url</c>),
    ///     returning decoded text for text/JSON/CSV attachments or size-capped base64 for binary.
    /// </summary>
    /// <param name="id">The attachment id.</param>
    /// <param name="maxContentBytes">
    ///     An optional client-side download cap: reading stops here and <c>Truncated</c> is set on the result.
    ///     <c>null</c> (the default) downloads the whole payload — Zendesk stores files up to 50 MB. Set a cap in
    ///     bounded contexts (e.g. tool responses). Must be positive when supplied.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskAttachmentContent> GetContentAsync(long id, int? maxContentBytes = null,
        CancellationToken cancellationToken = default);
}