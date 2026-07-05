using System.Net.Http.Headers;
using System.Text;
using ES.FX.Zendesk.Authentication;
using JetBrains.Annotations;

namespace ES.FX.Zendesk.Attachments;

/// <summary>
///     Downloads Zendesk attachment payloads from an attachment's <c>content_url</c> — fully by default, or capped
///     when the caller supplies <c>maxContentBytes</c>. Zendesk documents that <c>content_url</c> can point to
///     externally hosted files, so credentials are only sent when the URL resolves to the configured Zendesk
///     host — never to third parties. Attachment <em>metadata</em> is read via the generated
///     <c>ZendeskSupportApiClient</c> (<c>GET /api/v2/attachments/{id}</c>); this curated helper covers only the
///     content download, which the generated client cannot express.
/// </summary>
[PublicAPI]
public sealed class ZendeskAttachmentContentFetcher(HttpClient httpClient)
{
    private const int CopyBufferBytes = 8192;

    /// <summary>
    ///     Downloads the payload behind <paramref name="contentUrl" /> and decodes it to text when
    ///     <paramref name="contentType" /> declares a known text type (unknown/unsupported charsets fall back to
    ///     lossless base64), or base64 otherwise.
    /// </summary>
    /// <param name="contentUrl">The attachment's <c>content_url</c> (absolute).</param>
    /// <param name="contentType">The attachment's declared content type, used to pick text vs. base64 decoding.</param>
    /// <param name="maxContentBytes">An optional byte cap; when hit, the result is flagged as truncated.</param>
    /// <param name="offset">
    ///     The number of raw payload bytes to skip before capping/decoding, enabling ranged continuation of a
    ///     capped download: pass the previous call's <paramref name="offset" /> plus its
    ///     <see cref="ZendeskAttachmentContent.ReturnedBytes" />. The skip happens by reading and discarding
    ///     (attachment <c>content_url</c>s do not reliably honor HTTP <c>Range</c>), so the cost of a deep offset
    ///     is bandwidth, not memory. On UTF-8 text the capped tail always ends on a character boundary, so
    ///     chained continuations decode cleanly; a hand-picked <c>offset &gt; 0</c> — or any offset into
    ///     non-UTF-8 text — may start mid-character.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    public async Task<ZendeskAttachmentContent> DownloadAsync(string contentUrl, string? contentType,
        int? maxContentBytes = null, long offset = 0, CancellationToken cancellationToken = default)
    {
        if (maxContentBytes is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxContentBytes), maxContentBytes,
                "The download cap must be positive.");
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        var (bytes, truncated) = await DownloadBytesAsync(contentUrl, maxContentBytes, offset, cancellationToken)
            .ConfigureAwait(false);

        // Unknown/unsupported declared charsets fall back to base64 so the original bytes are never mis-decoded.
        var textEncoding = IsTextContentType(contentType) ? ResolveTextEncoding(contentType) : null;
        string content;
        string encodingLabel;
        int returnedBytes;
        if (textEncoding is not null)
        {
            var length = truncated && ReferenceEquals(textEncoding, Encoding.UTF8)
                ? TrimIncompleteUtf8Tail(bytes)
                : bytes.Length;
            content = textEncoding.GetString(bytes, 0, length);
            encodingLabel = "utf-8";
            returnedBytes = length;
        }
        else
        {
            content = Convert.ToBase64String(bytes);
            encodingLabel = "base64";
            returnedBytes = bytes.Length;
        }

        return new ZendeskAttachmentContent
        {
            Content = content,
            Encoding = encodingLabel,
            Truncated = truncated,
            ReturnedBytes = returnedBytes
        };
    }

    private async Task<(byte[] Bytes, bool Truncated)> DownloadBytesAsync(string contentUrl, int? maxContentBytes,
        long offset, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(contentUrl, UriKind.Absolute, out var uri))
            throw new InvalidOperationException(
                $"The attachment content URL '{contentUrl}' is not a valid absolute URL.");

        // content_url can legitimately point outside the tenant (externally hosted files). Send the tenant's
        // credentials only to the configured Zendesk host; fetch anything else anonymously, and only over HTTPS.
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!IsTrustedZendeskHost(uri))
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"The attachment content URL host '{uri.Host}' is not a Zendesk host and is not HTTPS; refusing to download.");
            request.Options.Set(ZendeskAuthenticationDelegatingHandler.SkipAuthentication, true);
        }

        using var response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        // Non-retryable failures already throw (typed) in the handler chain; this covers retry-exhausted statuses.
        await ZendeskResponseGuard.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();

        var chunk = new byte[CopyBufferBytes];

        // Skip-read the offset before capping/decoding (content_url downloads do not reliably honor HTTP
        // Range). An offset at or past the end of the payload yields an empty, untruncated result.
        var remainingToSkip = offset;
        while (remainingToSkip > 0)
        {
            var skipped = await stream
                .ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, remainingToSkip)), cancellationToken)
                .ConfigureAwait(false);
            if (skipped == 0) return ([], false);
            remainingToSkip -= skipped;
        }

        // No cap requested: download the whole (remaining) payload (Zendesk stores files up to 50 MB).
        if (maxContentBytes is null)
        {
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return (buffer.ToArray(), false);
        }

        var truncated = false;

        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            var remaining = maxContentBytes.Value - (int)buffer.Length;
            if (read < remaining)
            {
                buffer.Write(chunk, 0, read);
                continue;
            }

            buffer.Write(chunk, 0, remaining);
            // The cap is reached; the payload is only truncated if the stream genuinely has more data.
            truncated = read > remaining
                        || await stream.ReadAsync(chunk.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) > 0;
            break;
        }

        return (buffer.ToArray(), truncated);
    }

    // Credentials go only to the configured host — never to another *.zendesk.com tenant or a third party —
    // and never over a scheme less secure than the configured one (an http BaseUrl test double stays usable).
    private bool IsTrustedZendeskHost(Uri uri)
    {
        var baseAddress = httpClient.BaseAddress;
        if (baseAddress is null || !string.Equals(uri.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
               || string.Equals(uri.Scheme, baseAddress.Scheme, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;

        var media = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return media.StartsWith("text/", StringComparison.Ordinal)
               || media is "application/json" or "application/xml" or "application/csv" or "application/x-ndjson"
               || media.EndsWith("+json", StringComparison.Ordinal)
               || media.EndsWith("+xml", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Resolves the encoding declared by the content type's <c>charset</c> parameter (UTF-8 when absent), or
    ///     <c>null</c> when the declared charset is unknown so the caller returns lossless base64 instead of
    ///     mis-decoding the bytes.
    /// </summary>
    private static Encoding? ResolveTextEncoding(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return Encoding.UTF8;

        string? charset = null;
        if (MediaTypeHeaderValue.TryParse(contentType, out var parsed)) charset = parsed.CharSet?.Trim('"');
        if (string.IsNullOrWhiteSpace(charset)) return Encoding.UTF8;
        if (charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase)) return Encoding.UTF8;

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Returns the number of bytes to decode, dropping a trailing incomplete UTF-8 multi-byte sequence left by
    ///     the byte-level truncation cap so the decoded text ends at a clean code-point boundary instead of U+FFFD.
    /// </summary>
    private static int TrimIncompleteUtf8Tail(byte[] bytes)
    {
        var end = bytes.Length;
        var index = end - 1;
        var continuationBytes = 0;
        while (index >= 0 && continuationBytes < 3 && (bytes[index] & 0xC0) == 0x80)
        {
            index--;
            continuationBytes++;
        }

        if (index < 0) return end;

        var lead = bytes[index];
        int expectedContinuations;
        if ((lead & 0x80) == 0) expectedContinuations = 0;
        else if ((lead & 0xE0) == 0xC0) expectedContinuations = 1;
        else if ((lead & 0xF0) == 0xE0) expectedContinuations = 2;
        else if ((lead & 0xF8) == 0xF0) expectedContinuations = 3;
        else return end; // invalid lead byte — leave it to the decoder

        return continuationBytes < expectedContinuations ? index : end;
    }
}

/// <summary>
///     A downloaded attachment payload: the content (text, or base64 for binary/undecodable payloads), how it is
///     encoded, whether the download was truncated by a caller-supplied cap, and how many raw payload bytes the
///     content represents (the continuation unit for offset-based ranged downloads).
/// </summary>
[PublicAPI]
public sealed record ZendeskAttachmentContent
{
    /// <summary>The payload — decoded text when <see cref="Encoding" /> is <c>utf-8</c>, base64 otherwise.</summary>
    public required string Content { get; init; }

    /// <summary>How <see cref="Content" /> is encoded: <c>utf-8</c> (decoded text) or <c>base64</c>.</summary>
    public required string Encoding { get; init; }

    /// <summary>Whether the download stopped at the caller-supplied byte cap before the payload ended.</summary>
    public required bool Truncated { get; init; }

    /// <summary>
    ///     The number of raw payload bytes <see cref="Content" /> represents (after the requested offset). For
    ///     capped UTF-8 text this can be slightly under the cap: the cut is moved back to a clean character
    ///     boundary. Continue a truncated download by re-calling with <c>offset</c> = previous offset + this
    ///     value.
    /// </summary>
    public required int ReturnedBytes { get; init; }
}