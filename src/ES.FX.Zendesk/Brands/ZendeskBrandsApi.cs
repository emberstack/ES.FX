using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Brands;

/// <summary>
///     Default <see cref="IZendeskBrandsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskBrandsApi(HttpClient httpClient, ILogger<ZendeskBrandsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskBrandsApi
{
    /// <inheritdoc />
    public Task<ZendeskBrandsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("brands.json",
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor));
        return GetAsync<ZendeskBrandsResult>(requestUri, "Zendesk.Brands.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskBrand> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskBrandResponse>($"brands/{id}.json", "Zendesk.Brands.Get",
            cancellationToken).ConfigureAwait(false);
        return response.Brand ?? throw new InvalidOperationException($"Zendesk brand '{id}' was not found.");
    }

    /// <inheritdoc />
    public async Task<ZendeskBrand> CreateAsync(ZendeskBrandWrite brand,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskBrandResponse>("brands.json", new { brand }, "Zendesk.Brands.Create",
            cancellationToken).ConfigureAwait(false);
        return response.Brand ?? throw new InvalidOperationException("Zendesk returned no created brand.");
    }

    /// <inheritdoc />
    public async Task<ZendeskBrand> UpdateAsync(long id, ZendeskBrandWrite brand,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<ZendeskBrandResponse>($"brands/{id}.json", new { brand },
            "Zendesk.Brands.Update", cancellationToken).ConfigureAwait(false);
        return response.Brand ?? throw new InvalidOperationException($"Zendesk returned no brand for '{id}'.");
    }

    /// <inheritdoc />
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"brands/{id}.json", null, "Zendesk.Brands.Delete", cancellationToken);
}