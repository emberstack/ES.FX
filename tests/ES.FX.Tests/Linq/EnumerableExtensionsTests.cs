using ES.FX.Linq;

namespace ES.FX.Tests.Linq;


public class EnumerableExtensionsTests
{

    [Fact]
    public void Enumerable_GetManifestResources_ReturnsItemIfNotEmpty()
    {
        var enumerable = new[] { 1, 2, 3, 4, 5 };
        var result = enumerable.TakeRandomItemOrDefault();
        Assert.Contains(result, enumerable);
    }


    [Fact]
    public void Enumerable_GetManifestResources_ReturnsItemIfEmpty()
    {
        var enumerable = Array.Empty<int>();
        var result = enumerable.TakeRandomItemOrDefault();
        Assert.Equal(default, result);
    }



}