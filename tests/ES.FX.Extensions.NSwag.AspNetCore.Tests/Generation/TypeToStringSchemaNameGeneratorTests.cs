﻿using ES.FX.Extensions.NSwag.AspNetCore.Generation;

namespace ES.FX.Extensions.NSwag.AspNetCore.Tests.Generation;

public class TypeToStringSchemaNameGeneratorTests
{
    [Theory]
    [InlineData(typeof(string), "System.String")]
    [InlineData(typeof(int), "System.Int32")]
    [InlineData(typeof(int?), "System.Nullable`1[System.Int32]")]
    [InlineData(typeof(int[]), "System.Int32[]")]
    [InlineData(typeof(int[,]), "System.Int32[,]")]
    [InlineData(typeof(int[][]), "System.Int32[][]")]
    public void CanGenerateSchemaName_Primitives(Type type, string typeName)
    {
        Assert.Equal(new TypeToStringSchemaNameGenerator().Generate(type), typeName);
    }
}