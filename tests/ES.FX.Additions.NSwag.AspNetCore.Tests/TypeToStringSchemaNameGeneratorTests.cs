using ES.FX.Additions.NSwag.AspNetCore.Generation;
using NJsonSchema.Generation;

namespace ES.FX.Additions.NSwag.AspNetCore.Tests;

public class TypeToStringSchemaNameGeneratorTests
{
    private readonly TypeToStringSchemaNameGenerator _generator = new();

    [Fact]
    public void Implements_ISchemaNameGenerator() =>
        Assert.IsAssignableFrom<ISchemaNameGenerator>(_generator);

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(List<int>))]
    [InlineData(typeof(Dictionary<string, List<int>>))]
    public void Generate_returns_type_ToString_verbatim(Type type) =>
        Assert.Equal(type.ToString(), _generator.Generate(type));

    [Fact]
    public void Generic_type_retains_raw_backtick_and_bracket_syntax()
    {
        // Documents the difference vs. SanitizedSchemaNameGenerator: this generator does NOT
        // produce OpenAPI-component-valid names for generic types.
        var name = _generator.Generate(typeof(List<string>));

        Assert.Equal("System.Collections.Generic.List`1[System.String]", name);
    }
}