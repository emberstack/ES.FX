using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Users;

/// <summary>
///     Default <see cref="IZendeskUsersApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskUsersApi(HttpClient httpClient, ILogger<ZendeskUsersApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskUsersApi
{
    /// <summary>Zendesk's documented maximum for <c>show_many</c>; larger lists are rejected with 400.</summary>
    private const int MaxIdsPerShowManyRequest = 100;

    /// <inheritdoc />
    public async Task<ZendeskUser> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskUserResponse>("users/me.json", "Zendesk.Users.GetCurrent",
            cancellationToken).ConfigureAwait(false);
        return response.User
               ?? throw new InvalidOperationException("Zendesk returned an empty response for the current user.");
    }

    /// <inheritdoc />
    public async Task<ZendeskUser> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskUserResponse>($"users/{id}.json", "Zendesk.Users.Get", cancellationToken)
            .ConfigureAwait(false);
        return response.User ?? throw new InvalidOperationException($"Zendesk user '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskUsersResult> SearchAsync(string query, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("users/search.json",
            ("query", query), ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskUsersResult>(requestUri, "Zendesk.Users.Search", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskUsersResult> GetManyAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return new ZendeskUsersResult();

        if (ids.Count <= MaxIdsPerShowManyRequest)
        {
            var requestUri = ZendeskQuery.Build("users/show_many.json", ("ids", string.Join(',', ids)));
            return await GetAsync<ZendeskUsersResult>(requestUri, "Zendesk.Users.GetMany", cancellationToken)
                .ConfigureAwait(false);
        }

        // show_many rejects more than 100 ids with 400 Bad Request — chunk and merge instead of failing the batch.
        var users = new List<ZendeskUser>(ids.Count);
        for (var offset = 0; offset < ids.Count; offset += MaxIdsPerShowManyRequest)
        {
            var chunk = ids.Skip(offset).Take(MaxIdsPerShowManyRequest);
            var requestUri = ZendeskQuery.Build("users/show_many.json", ("ids", string.Join(',', chunk)));
            var page = await GetAsync<ZendeskUsersResult>(requestUri, "Zendesk.Users.GetMany", cancellationToken)
                .ConfigureAwait(false);
            users.AddRange(page.Users);
        }

        return new ZendeskUsersResult { Users = users, Count = users.Count };
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> GetRequestedTicketsAsync(long userId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"users/{userId}/tickets/requested.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Users.RequestedTickets", cancellationToken);
    }
}