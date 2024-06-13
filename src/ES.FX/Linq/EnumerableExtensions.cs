using JetBrains.Annotations;

namespace ES.FX.Linq;

/// <summary>
///     Linq extensions for <see cref="IEnumerable{T}"></see> collection
/// </summary>
[PublicAPI]
public static class EnumerableExtensions
{
    /// <summary>
    ///     Returns a random item in an <see cref="IList{T}"></see> collection or the default value
    /// </summary>
    /// <param name="source">The source enumerable</param>
    public static T? TakeRandomItemOrDefault<T>(this IEnumerable<T?> source)
    {
        var list = source.ToList();
        return list.Count == 0 ? default : list[Random.Shared.Next(list.Count)];
    }
}