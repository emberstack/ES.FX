using System.Net.Http.Headers;
using System.Text;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.Authentication;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Attachments;

/// <summary>
///     Default <see cref="IZendeskAttachmentsApi" /> implementation. Reads attachment metadata via the shared
///     Zendesk <see cref="HttpClient" /> and then streams the <c>content_url</c> — fully by default, or capped
///     when the caller supplies <c>maxContentBytes</c>. Zendesk documents that <c>content_url</c> can point to
///     externally hosted files, so credentials are only sent when the URL resolves to the configured Zendesk
///     host — never to third parties.
/// </summary>
internal sealed class ZendeskAttachmentsApi(HttpClient httpClient, ILogger<ZendeskAttachmentsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskAttachmentsApi
{
    private const int CopyBufferBytes = 8192;

    /// <inheritdoc />
    public async Task<ZendeskAttachmentContent> GetContentAsync(long id, int? maxContentBytes = null,
        CancellationToken cancellationToken = default)
    {
        if (maxContentBytes is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxContentBytes), maxContentBytes,
                "The download cap must be positive.");

        var response = await GetAsync<ZendeskAttachmentResponse>($"attachments/{id}.json", "Zendesk.Attachments.Get",
            cancellationToken).ConfigureAwait(false);
        var attachment = response.Attachment
                         ?? throw new InvalidOperationException($"Zendesk attachment '{id}' was not found.");

        if (string.IsNullOrWhiteSpace(attachment.ContentUrl))
            throw new InvalidOperationException($"Zendesk attachment '{id}' has no downloadable content URL.");

        var (bytes, truncated) = await DownloadAsync(attachment.ContentUrl, maxContentBytes, cancellationToken)
            .ConfigureAwait(false);

        // Unknown/unsupported declared charsets fall back to base64 so the original bytes are never mis-decoded.
        var textEncoding = IsTextContentType(attachment.ContentType)
            ? ResolveTextEncoding(attachment.ContentType)
            : null;
        string content;
        string encodingLabel;
        if (textEncoding is not null)
        {
            var length = truncated && ReferenceEquals(textEncoding, Encoding.UTF8)
                ? TrimIncompleteUtf8Tail(bytes)
                : bytes.Length;
            content = textEncoding.GetString(bytes, 0, length);
            encodingLabel = "utf-8";
        }
        else
        {
            content = Convert.ToBase64String(bytes);
            encodingLabel = "base64";
        }

        return new ZendeskAttachmentContent
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            Size = attachment.Size,
            Encoding = encodingLabel,
            Content = content,
            Truncated = truncated
        };
    }

    private async Task<(byte[] Bytes, bool Truncated)> DownloadAsync(string contentUrl, int? maxContentBytes,
        CancellationToken cancellationToken)
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

        using var response = await HttpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await ZendeskResponseGuard.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();

        // No cap requested: download the whole payload (Zendesk stores files up to 50 MB).
        if (maxContentBytes is null)
        {
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return (buffer.ToArray(), false);
        }

        var chunk = new byte[CopyBufferBytes];
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
        var baseAddress = HttpClient.BaseAddress;
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