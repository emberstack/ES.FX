using System.Reflection;
using JetBrains.Annotations;

namespace ES.FX.Reflection;

/// <summary>
///     Extension methods for <see cref="ManifestResource"></see>
/// </summary>
[PublicAPI]
public static class ManifestResourceExtensions
{
    /// <summary>
    ///     Gets the <see cref="ManifestResource"></see> wrappers for embedded assembly resources
    /// </summary>
    /// <param name="assembly">Source assembly</param>
    /// <returns> List of <see cref="ManifestResource"></see> wrappers</returns>
    public static ManifestResource[] GetManifestResources(this Assembly assembly)
    {
        return assembly.GetManifestResourceNames()
            .Select(resource => new ManifestResource(assembly, resource))
            .ToArray();
    }
}