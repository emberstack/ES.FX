using System.Net.Http.Headers;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Uploads;

/// <summary>
///     Default <see cref="IZendeskUploadsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
///     Uploads send the RAW file bytes (not JSON, not multipart), per Zendesk's uploads contract.
/// </summary>
internal sealed class ZendeskUploadsApi(HttpClient httpClient, ILogger<ZendeskUploadsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskUploadsApi
{
    /// <inheritdoc />
    public async Task<ZendeskUpload> UploadAsync(string fileName, ReadOnlyMemory<byte> content, string contentType,
        string? token = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var requestUri = ZendeskQuery.Build("uploads.json", ("filename", fileName), ("token", token));
        var body = new ByteArrayContent(content.ToArray());
        // Parse (not the ctor) so parameterized types like "text/plain; charset=utf-8" are accepted.
        body.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        var response = await SendAsync<ZendeskUploadResponse>(HttpMethod.Post, requestUri, body,
            "Zendesk.Uploads.Upload", cancellationToken).ConfigureAwait(false);
        return response.Upload ?? throw new InvalidOperationException("Zendesk returned no upload.");
    }

    /// <inheritdoc />
    public Task DeleteAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return SendAsync(HttpMethod.Delete, $"uploads/{Uri.EscapeDataString(token)}.json", null,
            "Zendesk.Uploads.Delete", cancellationToken);
    }
}