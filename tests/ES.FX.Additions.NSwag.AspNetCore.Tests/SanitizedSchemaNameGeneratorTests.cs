using System.Text.RegularExpressions;
using ES.FX.Additions.NSwag.AspNetCore.Generation;
using NJsonSchema.Generation;

namespace ES.FX.Additions.NSwag.AspNetCore.Tests;

public class SanitizedSchemaNameGeneratorTests
{
    /// <summary>The OpenAPI component-key pattern the sanitized output must satisfy.</summary>
    private static readonly Regex OpenApiComponentKey = new("^[a-zA-Z0-9.\\-_]+$", RegexOptions.Compiled);

    private readonly SanitizedSchemaNameGenerator _generator = new();

    [Fact]
    public void Implements_ISchemaNameGenerator() =>
        Assert.IsAssignableFrom<ISchemaNameGenerator>(_generator);

    [Fact]
    public void Simple_type_is_returned_verbatim() =>
        // No disallowed characters -> full type name is preserved exactly.
        Assert.Equal(typeof(int).ToString(), _generator.Generate(typeof(int)));

    [Fact]
    public void Simple_type_name_is_namespace_qualified() =>
        Assert.Equal("System.Int32", _generator.Generate(typeof(int)));

    [Fact]
    public void Simple_type_output_is_openapi_valid() =>
        Assert.Matches(OpenApiComponentKey, _generator.Generate(typeof(string)));

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(Dictionary<string, List<int>>))]
    [InlineData(typeof(Dictionary<string, Dictionary<int, List<string>>>))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(KeyValuePair<string, int>))]
    [InlineData(typeof(Outer.Inner))]
    [InlineData(typeof(Outer.InnerGeneric<int>))]
    [InlineData(typeof(GenericOuter<string>.NestedInGeneric))]
    public void Output_is_always_openapi_component_valid(Type type)
    {
        var name = _generator.Generate(type);

        Assert.False(string.IsNullOrEmpty(name));
        Assert.Matches(OpenApiComponentKey, name);
    }

    [Fact]
    public void Generic_type_has_no_disallowed_characters()
    {
        var name = _generator.Generate(typeof(Dictionary<string, List<int>>));

        // The raw Type.ToString() contains all of these; none may survive sanitization.
        Assert.DoesNotContain('`', name);
        Assert.DoesNotContain('[', name);
        Assert.DoesNotContain(']', name);
        Assert.DoesNotContain(',', name);
        Assert.DoesNotContain('+', name);
        Assert.DoesNotContain(' ', name);
    }

    [Fact]
    public void Generic_type_preserves_type_names_and_namespaces()
    {
        var name = _generator.Generate(typeof(Dictionary<string, List<int>>));

        // Descriptive shape is retained even though separators are sanitized.
        Assert.Contains("Dictionary", name);
        Assert.Contains("List", name);
        Assert.Contains("System.String", name);
        Assert.Contains("System.Int32", name);
    }

    [Fact]
    public void Nested_type_plus_separator_is_sanitized()
    {
        // Type.ToString() of a nested type uses '+' between outer and inner.
        var raw = typeof(Outer.Inner).ToString();
        Assert.Contains('+', raw);

        var name = _generator.Generate(typeof(Outer.Inner));
        Assert.DoesNotContain('+', name);
        Assert.Matches(OpenApiComponentKey, name);
        Assert.Contains("Outer", name);
        Assert.Contains("Inner", name);
    }

    [Fact]
    public void Output_is_stable_across_repeated_calls()
    {
        var type = typeof(Dictionary<string, List<int>>);

        var first = _generator.Generate(type);
        var second = _generator.Generate(type);
        var third = new SanitizedSchemaNameGenerator().Generate(type);

        Assert.Equal(first, second);
        Assert.Equal(first, third);
    }

    [Fact]
    public void Output_has_no_leading_or_trailing_underscore()
    {
        // The bracketed suffix of a generic type ends in ']' which sanitizes to a trailing
        // underscore; it must be trimmed.
        var name = _generator.Generate(typeof(List<int>));

        Assert.False(name.StartsWith('_'), $"unexpected leading underscore: {name}");
        Assert.False(name.EndsWith('_'), $"unexpected trailing underscore: {name}");
    }

    [Fact]
    public void Consecutive_disallowed_characters_collapse_to_single_underscore()
    {
        // Regression guard on the documented example from the XML docs:
        // List`1[System.String] -> ...List_1_System.String (single underscores, no doubles).
        var name = _generator.Generate(typeof(List<string>));

        Assert.DoesNotContain("__", name);
        Assert.Equal("System.Collections.Generic.List_1_System.String", name);
    }

    [Fact]
    public void Distinct_generic_instantiations_produce_distinct_names()
    {
        var listOfInt = _generator.Generate(typeof(List<int>));
        var listOfString = _generator.Generate(typeof(List<string>));

        Assert.NotEqual(listOfInt, listOfString);
    }

    // --- Test fixtures for nested-type behavior ---

    private class Outer
    {
        internal class Inner;

        internal class InnerGeneric<T>;
    }

    private class GenericOuter<T>
    {
        internal class NestedInGeneric;
    }
}
