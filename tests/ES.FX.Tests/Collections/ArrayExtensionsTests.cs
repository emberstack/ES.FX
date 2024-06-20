using ES.FX.Collections;

namespace ES.FX.Tests.Collections;

public class ArrayExtensionsTests
{
    [Fact]
    public void Array_NullOrEmpty_ReturnsFalseForArrayWithElements()
    {
        var array = new[] { 0 };
        var result = array.IsNullOrEmpty();
        Assert.False(result);
    }

    [Fact]
    public void Array_NullOrEmpty_ReturnsTrueForEmpty()
    {
        Array array = Array.Empty<int>();
        var result = array.IsNullOrEmpty();
        Assert.True(result);
    }

    [Fact]
    public void Array_NullOrEmpty_ReturnsTrueForNull()
    {
        Array? array = null;
        var result = array.IsNullOrEmpty();
        Assert.True(result);
    }
}