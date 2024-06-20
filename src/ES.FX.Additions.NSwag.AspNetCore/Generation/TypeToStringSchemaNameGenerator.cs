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
    public string Generate(Type type) => type.ToString();
}