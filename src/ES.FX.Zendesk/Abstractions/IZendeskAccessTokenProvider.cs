namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Provides (and caches) the OAuth bearer access token used to authenticate Zendesk API requests.
/// </summary>
public interface IZendeskAccessTokenProvider
{
    /// <summary>
    ///     Returns a valid OAuth access token, acquiring or refreshing it as needed.
    /// </summary>
    /// <param name="forceRefresh">
    ///     When <c>true</c>, bypasses the cache and acquires a fresh token (used to recover from a <c>401</c>).
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<string> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}