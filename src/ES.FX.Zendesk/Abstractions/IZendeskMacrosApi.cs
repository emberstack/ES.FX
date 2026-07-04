using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>macros</c> resource (canned responses / bulk actions).
/// </summary>
public interface IZendeskMacrosApi
{
    /// <summary>Lists macros (<c>GET /api/v2/macros.json</c>).</summary>
    Task<ZendeskMacrosResult> ListAsync(int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a macro by id, including its actions (<c>GET /api/v2/macros/{id}.json</c>).</summary>
    Task<ZendeskMacro> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists only the macros usable by the current agent (<c>GET /api/v2/macros/active.json</c>).
    /// </summary>
    Task<ZendeskMacrosResult> ListActiveAsync(int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a macro (<c>POST /api/v2/macros.json</c>). <c>Title</c> and <c>Actions</c> are required.</summary>
    Task<ZendeskMacro> CreateAsync(ZendeskMacroWrite macro, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a macro (<c>PUT /api/v2/macros/{id}.json</c>). WARNING: sending <c>Actions</c> replaces the
    ///     whole array — include ALL actions when changing any one.
    /// </summary>
    Task<ZendeskMacro> UpdateAsync(long id, ZendeskMacroWrite macro, CancellationToken cancellationToken = default);

    /// <summary>Deletes a macro (<c>DELETE /api/v2/macros/{id}.json</c>).</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}