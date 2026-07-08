using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData;

/// <summary>
///     Fluent builder returned by <c>AddOpenData()</c>. Each dataset/client package adds an
///     <c>Add{Scope}{Leaf}()</c> extension method on this type, so registration reads as one chained manifest.
/// </summary>
[PublicAPI]
public interface IOpenDataBuilder
{
    /// <summary>The underlying service collection, for dataset packages to register into.</summary>
    IServiceCollection Services { get; }
}
