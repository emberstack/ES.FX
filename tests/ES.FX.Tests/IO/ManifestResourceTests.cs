using ES.FX.IO;

namespace ES.FX.Tests.IO;


public class ManifestResourceTests
{
    [Fact]
    public void Stream_ToByteArray_ReturnsCorrectByteArray()
    {
        var sourceArray = new[] { byte.MinValue, byte.MaxValue };
        var source = new MemoryStream(sourceArray);

        var result = source.ToByteArray();
        Assert.Equal(2, result.Length);
        Assert.Equal(sourceArray, result);
    }

    [Fact]
    public void Stream_ToByteArray_ReturnsCorrectByteArrayForNonMemoryStream()
    {
        var sourceArray = new[] { byte.MinValue, byte.MaxValue };
        var sourceMemoryStream = new MemoryStream(sourceArray);
        var source = new BufferedStream(sourceMemoryStream);

        var result = source.ToByteArray();
        Assert.Equal(2, result.Length);
        Assert.Equal(sourceArray, result);
    }


    [Fact]
    public async Task Stream_ToByteArrayAsync_ReturnsCorrectByteArray()
    {
        var sourceArray = new[] { byte.MinValue, byte.MaxValue };
        var source = new MemoryStream(sourceArray);

        var result = await source.ToByteArrayAsync();
        Assert.Equal(2, result.Length);
        Assert.Equal(sourceArray, result);
    }


    [Fact]
    public async Task Stream_ToByteArrayAsync_ReturnsCorrectByteArrayForNonMemoryStream()
    {
        var sourceArray = new[] { byte.MinValue, byte.MaxValue };
        var sourceMemoryStream = new MemoryStream(sourceArray);
        var source = new BufferedStream(sourceMemoryStream);

        var result = await source.ToByteArrayAsync();
        Assert.Equal(2, result.Length);
        Assert.Equal(sourceArray, result);
    }


}