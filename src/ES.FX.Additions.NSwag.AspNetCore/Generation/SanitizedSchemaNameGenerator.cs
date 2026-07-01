using System.Text;
using JetBrains.Annotations;
using NJsonSchema.Generation;

namespace ES.FX.Additions.NSwag.AspNetCore.Generation;

/// <summary>
///     Generates OpenAPI-valid schema names from <see cref="Type.ToString" /> by sanitizing characters that
///     are not permitted in an OpenAPI component key.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Type.ToString" /> produces namespace-qualified, generic-aware names that favour
///         uniqueness, but they contain characters the OpenAPI component key pattern (<c>^[a-zA-Z0-9.\-_]+$</c>)
///         does not allow. Generic types include backtick arity and bracketed type arguments (e.g.
///         <c>System.Collections.Generic.List`1[System.String]</c>) and nested types use a <c>+</c> separator.
///     </para>
///     <para>
///         This generator keeps the descriptive shape of the full type name while replacing every character
///         outside the allowed set (backticks, brackets, commas, plus signs, and any other disallowed
///         character) with an underscore. Consecutive disallowed characters collapse into a single underscore
///         and leading and trailing underscores are trimmed, yielding compact names that always match
///         <c>^[a-zA-Z0-9.\-_]+$</c>.
///     </para>
/// </remarks>
[PublicAPI]
public class SanitizedSchemaNameGenerator : ISchemaNameGenerator
{
    /// <summary>
    ///     Generates an OpenAPI-valid schema name for the specified <paramref name="type" /> by sanitizing the
    ///     result of <see cref="Type.ToString" />.
    /// </summary>
    /// <param name="type">The type to generate the schema name for.</param>
    /// <returns>
    ///     A schema name derived from <see cref="Type.ToString" /> in which every character outside the OpenAPI
    ///     component key pattern <c>^[a-zA-Z0-9.\-_]+$</c> has been replaced with an underscore, with
    ///     consecutive replacements collapsed and leading and trailing underscores removed. For example,
    ///     <c>System.Collections.Generic.List`1[System.String]</c> becomes
    ///     <c>System.Collections.Generic.List_1_System.String</c>.
    /// </returns>
    public string Generate(Type type) => Sanitize(type.ToString());

    private static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        var lastWasUnderscore = false;

        foreach (var character in name)
            if (IsAllowed(character))
            {
                builder.Append(character);
                lastWasUnderscore = false;
            }
            else if (!lastWasUnderscore)
            {
                builder.Append('_');
                lastWasUnderscore = true;
            }

        return builder.ToString().Trim('_');
    }

    private static bool IsAllowed(char character) =>
        character is >= 'a' and <= 'z' ||
        character is >= 'A' and <= 'Z' ||
        character is >= '0' and <= '9' ||
        character is '.' or '-' or '_';
}