using JetBrains.Annotations;
using k8s;
using k8s.Models;

namespace ES.FX.Extensions.KubernetesClient.Models.Extensions;

[PublicAPI]
public static class V1ObjectReferenceExtensions
{
    public static V1ObjectReference ObjectReference(this IKubernetesObject<V1ObjectMeta> obj) =>
        new()
        {
            ApiVersion = obj.ApiVersion,
            Kind = obj.Kind,
            Name = obj.Name(),
            NamespaceProperty = obj.Namespace(),
            ResourceVersion = obj.ResourceVersion(),
            Uid = obj.Uid()
        };

    /// <summary>
    ///     Returns a <see cref="V1ObjectReference" /> from a <see cref="V1ObjectMeta" />.
    /// </summary>
    /// <remarks>Not all properties are populated when using the <see cref="V1ObjectMeta" /> as the source.</remarks>
    /// <param name="metadata">The metadata used as the source for the reference.</param>
    public static V1ObjectReference ObjectReference(this V1ObjectMeta metadata) => new()
    {
        Name = metadata.Name,
        NamespaceProperty = metadata.NamespaceProperty,
        Uid = metadata.Uid,
        ResourceVersion = metadata.ResourceVersion
    };

    /// <summary>
    ///     Returns a <see cref="NamespacedName" />> for the object
    /// </summary>
    public static NamespacedName NamespacedName(this V1ObjectReference? reference) =>
        new(reference?.NamespaceProperty ?? string.Empty,
            reference?.Name ?? string.Empty);
}