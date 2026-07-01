using System.Net.Http.Json;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ES.FX.Zendesk.Authentication;

/// <summary>
///     Acquires and caches a Zendesk OAuth access token using the <c>client_credentials</c> grant. Registered as a
///     singleton (per instance): the cache and the refresh lock are shared across all requests for that instance.
/// </summary>
internal sealed class ClientCredentialsAccessTokenProvider : IZendeskAccessTokenProvider, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ClientCredentialsAccessTokenProvider> _logger;
    private readonly IOptionsMonitor<ZendeskClientOptions> _options;
    private readonly string _optionsName;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private readonly string _tokenClientName;

    private CachedToken? _cache;

    public ClientCredentialsAccessTokenProvider(
        IHttpClientFactory httpClientFactory,
        string tokenClientName,
        IOptionsMonitor<ZendeskClientOptions> options,
        string optionsName,
        TimeProvider timeProvider,
        ILogger<ClientCredentialsAccessTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenClientName = tokenClientName;
        _options = options;
        _optionsName = optionsName;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public void Dispose() => _refreshLock.Dispose();

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        // Fast path: a valid cached token, no lock taken.
        var cached = _cache;
        if (!forceRefresh && cached is not null && cached.IsValidAt(_timeProvider.GetUtcNow()))
            return cached.AccessToken;

        // The token this caller is trying to move past. A forced refresh only needs to *supersede* this
        // specific token, so a concurrent refresher that already replaced it satisfies us too (below).
        var stale = cached;

        // Slow path: serialize refreshers so only one hits the token endpoint (no stampede).
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check: another caller may have refreshed while we waited on the lock. Reuse their token
            // if it is now valid AND is not the same stale token we set out to replace. This also covers the
            // force-refresh path (a 401 storm): the first waiter refreshes and every other waiter reuses that
            // one token instead of each issuing a redundant token request.
            cached = _cache;
            if (cached is not null && cached.IsValidAt(_timeProvider.GetUtcNow()) && !ReferenceEquals(cached, stale))
                return cached.AccessToken;

            var fresh = await RequestTokenAsync(cancellationToken).ConfigureAwait(false);
            _cache = fresh;
            return fresh.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<CachedToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var options = _options.Get(_optionsName);
        var oauth = options.OAuth;

        if (string.IsNullOrWhiteSpace(oauth.ClientId) || string.IsNullOrWhiteSpace(oauth.ClientSecret))
            throw new InvalidOperationException(
                "Zendesk OAuth authentication requires an OAuth ClientId and ClientSecret.");

        var payload = new ClientCredentialsTokenRequest
        {
            ClientId = oauth.ClientId,
            ClientSecret = oauth.ClientSecret,
            Scope = string.IsNullOrWhiteSpace(oauth.Scope) ? null : oauth.Scope,
            ExpiresIn = oauth.ExpiresIn
        };

        // Uses a dedicated token client that has NO auth handler attached, so this call does not recurse.
        var client = _httpClientFactory.CreateClient(_tokenClientName);
        using var response = await client
            .PostAsJsonAsync(options.GetOAuthTokenEndpoint(), payload, cancellationToken).ConfigureAwait(false);
        await ZendeskResponseGuard.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var token = await response.Content
            .ReadFromJsonAsync<ClientCredentialsTokenResponse>(cancellationToken).ConfigureAwait(false);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException("The Zendesk token endpoint returned no access token.");

        var lifetime = token.ExpiresIn > 0 ? TimeSpan.FromSeconds(token.ExpiresIn) : TimeSpan.FromMinutes(30);
        var expiresAt = _timeProvider.GetUtcNow() + lifetime - oauth.ExpiryBuffer;

        _logger.LogDebug("Acquired Zendesk OAuth access token (expires in {ExpiresInSeconds}s)", token.ExpiresIn);
        return new CachedToken(token.AccessToken!, expiresAt);
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt)
    {
        public bool IsValidAt(DateTimeOffset now) => now < ExpiresAt;
    }
}