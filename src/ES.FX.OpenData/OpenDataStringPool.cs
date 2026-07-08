using JetBrains.Annotations;

namespace ES.FX.OpenData;

/// <summary>
///     A transient build-time string pool. Reference data contains many byte-identical strings (repeated
///     locality names, display names equal to canonical names); interning them through a pool during store
///     construction collapses the duplicates to a single instance. Discard the pool after the store is frozen.
///     Unlike <see cref="string.Intern(string)" />, this pool is collectable and process-local.
/// </summary>
[PublicAPI]
public sealed class OpenDataStringPool
{
    private readonly Dictionary<string, string> _pool = new(StringComparer.Ordinal);

    /// <summary>Returns the pooled instance equal to <paramref name="value" />, adding it if not yet present.</summary>
    public string Intern(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (_pool.TryGetValue(value, out var existing)) return existing;
        _pool[value] = value;
        return value;
    }
}
