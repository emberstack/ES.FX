using ES.FX.Reflection;

namespace ES.FX.Tests.Reflection;


public class EnumerableExtensionsTests
{

    [Fact]
    public void Assembly_GetManifestResources_ReturnsManifestResources()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resources = assembly.GetManifestResources();
        Assert.NotEmpty(resources);
    }


    [Fact]
    public void ManifestResource_ReturnsNullInfoForInvalidResourceName()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resource = new ManifestResource(assembly, Guid.NewGuid().ToString());
        Assert.Null(resource.Info);
    }

    [Fact]
    public void ManifestResource_ReturnsNullStreamForInvalidResourceName()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resource = new ManifestResource(assembly, Guid.NewGuid().ToString());
        Assert.Null(resource.GetStream());
    }

    [Fact]
    public void ManifestResource_ReturnsNullStreamReaderForInvalidResourceName()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resource = new ManifestResource(assembly, Guid.NewGuid().ToString());
        Assert.Null(resource.GetStreamReader());
    }

    [Fact]
    public void ManifestResource_ReturnsNullByteArrayForInvalidResourceName()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resource = new ManifestResource(assembly, Guid.NewGuid().ToString());
        var result = resource.ReadAllBytes();
        Assert.Null(result);
    }

    [Fact]
    public void ManifestResource_ReturnsNullTextForInvalidResourceName()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resource = new ManifestResource(assembly, Guid.NewGuid().ToString());
        var result = resource.ReadText();
        Assert.Null(result);
    }

    [Fact]
    public async Task ManifestResource_ReturnsNullTextAsyncForInvalidResourceName()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resource = new ManifestResource(assembly, Guid.NewGuid().ToString());
        var result = await resource.ReadTextAsync();
        Assert.Null(result);
    }



    [Fact]
    public void ManifestResource_ReturnsResourceByName()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resources = assembly.GetManifestResources();
        var resource = resources.First();

        var result = new ManifestResource(assembly, resource.Name);
        Assert.NotNull(result);
        Assert.Equal(resource.Name, result.Name);
        Assert.Equal(resource.Info?.FileName, result.Info?.FileName);
    }

    [Fact]
    public void ManifestResource_CanReadStream()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resources = assembly.GetManifestResources();
        var resource = resources.First();

        var result = resource.GetStream();
        Assert.NotNull(result);

        Assert.True(result.CanRead);
    }

    [Fact]
    public void ManifestResource_CanGetStreamReader()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resources = assembly.GetManifestResources();
        var resource = resources.First();

        var result = resource.GetStreamReader();
        Assert.NotNull(result);
    }

    [Fact]
    public void ManifestResource_CanReadText()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resources = assembly.GetManifestResources();
        var resource = resources.First();

        var result = resource.ReadText();
        Assert.NotNull(result);
        Assert.Equal("TestContentDoNotModify", result);
    }




    [Fact]
    public async Task ManifestResource_CanReadTextAsync()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resources = assembly.GetManifestResources();
        var resource = resources.First();

        var result = await resource.ReadTextAsync();
        Assert.NotNull(result);
        Assert.Equal("TestContentDoNotModify", result);
    }


    [Fact]
    public void ManifestResource_CanReadAllBytes()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resources = assembly.GetManifestResources();
        var resource = resources.First();

        var result = resource.ReadAllBytes();
        Assert.NotNull(result);
    }


    [Fact]
    public async Task ManifestResource_CanReadAllBytesAsync()
    {
        var assembly = typeof(EnumerableExtensionsTests).Assembly;
        var resources = assembly.GetManifestResources();
        var resource = resources.First();

        var result = await resource.ReadAllBytesAsync();
        Assert.NotNull(result);
    }


}