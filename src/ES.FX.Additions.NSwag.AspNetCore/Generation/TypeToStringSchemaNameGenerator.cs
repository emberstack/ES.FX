using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NJsonSchema.Generation;

namespace ES.FX.Additions.NSwag.AspNetCore.Generation;

/// <summary>
///     Generates schema names based on Type.ToString()
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public class TypeToStringSchemaNameGenerator : ISchemaNameGenerator
{
    /// <summary>
    ///     Generates the schema name for the specified <paramref name="type" /> using <see cref="Type.ToString" />
    /// </summary>
    /// <param name="type">The type to generate the schema name for</param>
    /// <returns>
    ///     The result of <see cref="Type.ToString" />. Note that generic types include backtick arity and bracketed
    ///     type arguments (e.g. <c>System.Collections.Generic.List`1[System.String]</c>) and nested types include a
    ///     <c>+</c> separator, which may not conform to the OpenAPI component key pattern
    /// </returns>
    public string Generate(Type type) => type.ToString();
}