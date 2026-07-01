using System.Collections.Concurrent;
using System.ComponentModel;
using JetBrains.Annotations;
using k8s;
using k8s.Models;

namespace ES.FX.Additions.KubernetesClient.Models.Extensions;

[PublicAPI]
public static class V1ObjectMetaExtensions
{
    /// <summary>
    ///     Private cache of type converters used to convert string values
    /// </summary>
    private static readonly ConcurrentDictionary<Type, TypeConverter> Converters = new();


    /// <summary>
    ///     Returns a <see cref="NamespacedName" /> for the object
    /// </summary>
    public static NamespacedName NamespacedName(this IKubernetesObject<V1ObjectMeta>? obj) =>
        new(obj?.Namespace() ?? string.Empty, obj?.Name() ?? string.Empty);


    /// <summary>
    ///     Tries to read the annotation value and convert it to the type specified.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="metadata">Source metadata</param>
    /// <param name="key">The annotation key</param>
    /// <param name="value">The resulting value</param>
    public static bool TryGetAnnotationValue<T>(this V1ObjectMeta metadata, string key, out T? value)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(key);

        value = default;

        if (metadata.Annotations is null || !metadata.Annotations.TryGetValue(key, out var raw)) return false;
        try
        {
            var conv = Converters.GetOrAdd(typeof(T), static t => TypeDescriptor.GetConverter(t));

            value = (T?)conv.ConvertFromInvariantString(raw.Trim());

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}