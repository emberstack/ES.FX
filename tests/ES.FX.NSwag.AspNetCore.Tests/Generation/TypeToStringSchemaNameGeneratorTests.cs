using ES.FX.NSwag.AspNetCore.Generation;

namespace ES.FX.NSwag.AspNetCore.Tests.Generation
{
    public class TypeToStringSchemaNameGeneratorTests
    {
        [Theory]
        [InlineData(typeof(string), "System.String")]
        [InlineData(typeof(int), "System.Int32")]
        [InlineData(typeof(int?), "System.Nullable`1[System.Int32]")]
        [InlineData(typeof(int[]), "System.Int32[]")]
        [InlineData(typeof(int[,]), "System.Int32[,]")]
        [InlineData(typeof(int[][]), "System.Int32[][]")]
        public void TestSchemaNameGeneration(Type type, string typeName)
        {
            Assert.Equal(new TypeToStringSchemaNameGenerator().Generate(type), typeName);
            
        }
    }
}
