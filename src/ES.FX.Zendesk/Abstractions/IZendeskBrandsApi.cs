using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>brands</c> resource — decodes the <c>brand_id</c> carried on tickets
///     (multibrand accounts).
/// </summary>
public interface IZendeskBrandsApi
{
    /// <summary>Lists brands (<c>GET /api/v2/brands.json</c>; cursor-paginated).</summary>
    Task<ZendeskBrandsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a brand by id (<c>GET /api/v2/brands/{id}.json</c>).</summary>
    Task<ZendeskBrand> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Creates a brand (<c>POST /api/v2/brands.json</c>; admin-only). <c>Name</c> and <c>Subdomain</c> are required.</summary>
    Task<ZendeskBrand> CreateAsync(ZendeskBrandWrite brand, CancellationToken cancellationToken = default);

    /// <summary>Updates a brand (<c>PUT /api/v2/brands/{id}.json</c>; admin-only).</summary>
    Task<ZendeskBrand> UpdateAsync(long id, ZendeskBrandWrite brand, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a brand (<c>DELETE /api/v2/brands/{id}.json</c>; set another brand default first).</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}