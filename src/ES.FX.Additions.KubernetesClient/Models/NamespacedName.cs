using JetBrains.Annotations;
using k8s;
using k8s.Models;

namespace ES.FX.Additions.KubernetesClient.Models;

/// <summary>
///     Represents a resource in namespace?/name format
/// </summary>
[PublicAPI]
public sealed record NamespacedName
{
    /// <summary>
    ///     Empty value. This should be used if the resource cannot be referenced to
    /// </summary>
    public static readonly NamespacedName Empty = new(string.Empty, string.Empty);

    private readonly string _name = string.Empty;

    private readonly string _namespace = string.Empty;


    /// <summary>
    ///     Creates a new <see cref="NamespacedName" /> from a namespace and a name
    /// </summary>
    /// <param name="ns">The namespace of the resource. Null or whitespace values are treated as empty</param>
    /// <param name="name">The name of the resource. Null or whitespace values are treated as empty</param>
    public NamespacedName(string? ns, string? name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        Namespace = string.IsNullOrWhiteSpace(ns) ? string.Empty : ns.Trim();
    }

    /// <summary>
    ///     Creates a new <see cref="NamespacedName" /> by parsing a value in namespace?/name format
    /// </summary>
    /// <param name="value">The value to parse</param>
    /// <exception cref="ArgumentException">Thrown when the value cannot be parsed</exception>
    public NamespacedName(string? value)
    {
        if (!TryParse(value, out var nsName))
            throw new ArgumentException("Could not parse the value into a valid namespaced name", nameof(value));

        Namespace = nsName.Namespace;
        Name = nsName.Name;
    }

    /// <summary>
    ///     Creates a new <see cref="NamespacedName" /> from a <see cref="V1ObjectMeta" />
    /// </summary>
    /// <param name="metadata">The metadata used as the source</param>
    public NamespacedName(V1ObjectMeta metadata) : this(metadata.Namespace(), metadata.Name)
    {
    }

    /// <summary>
    ///     Creates a new <see cref="NamespacedName" /> from an <see cref="IKubernetesObject{TMetadata}" />
    /// </summary>
    /// <param name="obj">The object used as the source</param>
    public NamespacedName(IKubernetesObject<V1ObjectMeta> obj) : this(obj.Namespace(), obj.Name())
    {
    }

    /// <summary>
    ///     The namespace of the resource. Empty for cluster-scoped resources
    /// </summary>
    public string Namespace
    {
        get => _namespace;
        init => _namespace = value.Trim();
    }

    /// <summary>
    ///     The name of the resource
    /// </summary>
    public string Name
    {
        get => _name;
        init => _name = value.Trim();
    }


    public bool Equals(NamespacedName? other)
    {
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Namespace, other?.Namespace) && string.Equals(Name, other?.Name);
    }


    /// <summary>
    ///     Tries to parse a value in namespace?/name format into a <see cref="NamespacedName" />
    /// </summary>
    /// <param name="value">The value to parse</param>
    /// <param name="nsName">The resulting <see cref="NamespacedName" />. <see cref="Empty" /> when parsing fails</param>
    /// <returns>True if the value was parsed successfully; otherwise false</returns>
    public static bool TryParse(string? value, out NamespacedName nsName)
    {
        nsName = Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var split = value.Split('/', StringSplitOptions.TrimEntries);
        switch (split.Length)
        {
            case 2 when split[0].Length > 0 && split[1].Length > 0:
                nsName = new NamespacedName(split[0], split[1]);
                return true;
            case 1 when split[0].Length > 0:
                nsName = new NamespacedName(string.Empty, split[0]);
                return true;
            default: return false;
        }
    }


    public override int GetHashCode() => HashCode.Combine(Namespace, Name);

    public override string ToString() => $"{(string.IsNullOrEmpty(Namespace) ? string.Empty : $"{Namespace}/")}{Name}";
}