using System.ComponentModel;
using JetBrains.Annotations;
using k8s;
using k8s.Models;

namespace ES.FX.KubernetesClient.Models.Extensions;

[PublicAPI]
public static class V1ObjectMetaExtensions
{
    /// <summary>
    ///     Private cache of type converters used to convert string values
    /// </summary>
    private static readonly Dictionary<Type, TypeConverter> Converters = [];


    /// <summary>
    ///     Returns a <see cref="NamespacedName" />> for the object
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
        value = default;

        var annotations = metadata.EnsureAnnotations();
        if (!annotations.TryGetValue(key, out var raw)) return false;
        try
        {
            if (Nullable.GetUnderlyingType(typeof(T)) != null)
            {
                if (!Converters.TryGetValue(typeof(T), out var conv))
                {
                    conv = TypeDescriptor.GetConverter(typeof(T));
                    Converters.TryAdd(typeof(T), conv);
                }

                value = (T?)conv.ConvertFromString(raw.Trim());
            }
            else
            {
                value = (T)Convert.ChangeType(raw.Trim(), typeof(T));
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}