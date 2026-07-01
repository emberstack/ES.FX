using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Macros;

/// <summary>
///     Default <see cref="IZendeskMacrosApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskMacrosApi(HttpClient httpClient, ILogger<ZendeskMacrosApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskMacrosApi
{
    /// <inheritdoc />
    public Task<ZendeskMacrosResult> ListAsync(int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("macros.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskMacrosResult>(requestUri, "Zendesk.Macros.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskMacro> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskMacroResponse>($"macros/{id}.json", "Zendesk.Macros.Get",
            cancellationToken).ConfigureAwait(false);
        return response.Macro ?? throw new InvalidOperationException($"Zendesk macro '{id}' was not found.");
    }
}