using JetBrains.Annotations;
using k8s;
using k8s.Models;

namespace ES.FX.KubernetesClient.Models;

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


    public NamespacedName(string? ns, string? name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        Namespace = string.IsNullOrWhiteSpace(ns) ? string.Empty : ns.Trim();
    }

    public NamespacedName(string? value)
    {
        if (!TryParse(value, out var nsName))
            throw new ArgumentException("Could not parse the value into a valid namespaced name", nameof(value));

        Namespace = nsName.Namespace;
        Name = nsName.Name;
    }

    public NamespacedName(V1ObjectMeta metadata) : this(metadata.Namespace(), metadata.Name)
    {
    }

    public NamespacedName(IKubernetesObject<V1ObjectMeta> obj) : this(obj.Namespace(), obj.Name())
    {
    }

    public string Namespace
    {
        get => _namespace;
        init => _namespace = value.Trim();
    }

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


    public static bool TryParse(string? value, out NamespacedName nsName)
    {
        nsName = Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var split = value.Trim().Split(["/"], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).ToList();
        switch (split.Count)
        {
            case 2:
                nsName = new NamespacedName(split.First(), split.Last());
                return true;
            case 1:
                nsName = new NamespacedName(string.Empty, split.Single());
                return true;
            default: return false;
        }
    }


    public override int GetHashCode()
    {
        unchecked
        {
            return ((!string.IsNullOrEmpty(Namespace) ? Namespace.GetHashCode() : 0) * 397) ^
                   (string.IsNullOrEmpty(Name) ? Name.GetHashCode() : 0);
        }
    }

    public override string ToString() => $"{(string.IsNullOrEmpty(Namespace) ? string.Empty : $"{Namespace}/")}{Name}";
}