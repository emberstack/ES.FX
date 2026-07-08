using JetBrains.Annotations;

namespace ES.FX.OpenData;

/// <summary>Shared helpers for the OpenData HTTP clients: bounded response-body capture and <c>Retry-After</c> parsing.</summary>
[PublicAPI]
public static class OpenDataHttp
{
    /// <summary>The default maximum length captured from a response body for inclusion in a typed exception.</summary>
    public const int MaxResponseBodyLength = 2048;

    /// <summary>Truncates a response body to at most <paramref name="maxLength" /> characters (for exception detail).</summary>
    public static string? Truncate(string? value, int maxLength = MaxResponseBodyLength) =>
        value is null ? null : value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>
    ///     Reads the server-requested retry delay from the <c>Retry-After</c> header. A delta is returned as-is;
    ///     an HTTP-date is converted to a delay from now; past values clamp to <see cref="TimeSpan.Zero" />.
    /// </summary>
    public static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null) return null;
        if (retryAfter.Delta is { } delta) return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        if (retryAfter.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }

        return null;
    }
}
