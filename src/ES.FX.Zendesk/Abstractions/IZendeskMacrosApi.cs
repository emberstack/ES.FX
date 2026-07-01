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
}