using ES.FX.Additions.Microsoft.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.Tests;

public class ModelBuilderConfigureExtensionTests
{
    [Fact]
    public void Constructor_StoresActions()
    {
        Action<ModelBuilder, DbContextOptions> a = (_, _) => { };
        Action<ModelBuilder, DbContextOptions> b = (_, _) => { };

        var extension = new ModelBuilderConfigureExtension(a, b);

        Assert.Equal(2, extension.ConfigureActions.Count);
        Assert.Same(a, extension.ConfigureActions[0]);
        Assert.Same(b, extension.ConfigureActions[1]);
    }

    [Fact]
    public void Constructor_NullActions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ModelBuilderConfigureExtension(null!));
    }

    [Fact]
    public void Constructor_NoActions_EmptyList()
    {
        var extension = new ModelBuilderConfigureExtension();
        Assert.Empty(extension.ConfigureActions);
    }

    [Fact]
    public void Validate_IsNoOp_DoesNotThrow()
    {
        var extension = new ModelBuilderConfigureExtension();
        // Passing null is acceptable because Validate is documented as a no-op.
        var exception = Record.Exception(() => extension.Validate(null!));
        Assert.Null(exception);
    }

    [Fact]
    public void ApplyServices_IsNoOp_DoesNotThrow()
    {
        var extension = new ModelBuilderConfigureExtension();
        var exception = Record.Exception(() => extension.ApplyServices(null!));
        Assert.Null(exception);
    }

    [Fact]
    public void Info_IsNotNull()
    {
        var extension = new ModelBuilderConfigureExtension();
        Assert.NotNull(extension.Info);
    }

    [Fact]
    public void ExtensionInfo_IsDatabaseProvider_False()
    {
        var extension = new ModelBuilderConfigureExtension();
        Assert.False(extension.Info.IsDatabaseProvider);
    }

    [Fact]
    public void ExtensionInfo_LogFragment_IsEmpty()
    {
        var extension = new ModelBuilderConfigureExtension();
        Assert.Equal(string.Empty, extension.Info.LogFragment);
    }

    [Fact]
    public void ExtensionInfo_ServiceProviderHashCode_IsZero()
    {
        // Documented footgun: hash is always 0 so EF Core treats differently-configured
        // options of the same context type as sharing one cached model.
        var extension = new ModelBuilderConfigureExtension((_, _) => { });
        Assert.Equal(0, extension.Info.GetServiceProviderHashCode());
    }

    [Fact]
    public void ExtensionInfo_ShouldUseSameServiceProvider_AlwaysTrue()
    {
        var a = new ModelBuilderConfigureExtension((_, _) => { });
        var b = new ModelBuilderConfigureExtension((_, _) => { });
        Assert.True(a.Info.ShouldUseSameServiceProvider(b.Info));
    }

    [Fact]
    public void ExtensionInfo_PopulateDebugInfo_IsNoOp_LeavesDictionaryEmpty()
    {
        var extension = new ModelBuilderConfigureExtension();
        var debugInfo = new Dictionary<string, string>();
        extension.Info.PopulateDebugInfo(debugInfo);
        Assert.Empty(debugInfo);
    }

    [Fact]
    public void ModelCacheFootgun_SecondConfigurationForSameContextType_IsSilentlyIgnored()
    {
        // Two options instances of the SAME DbContext type but with DIFFERENT actions.
        // Because GetServiceProviderHashCode() == 0 and ShouldUseSameServiceProvider() == true, and the
        // model is cached per-context-type + provider, whichever builds the model first wins.
        var name = Guid.NewGuid().ToString();

        var first = new DbContextOptionsBuilder<FootgunContext>()
            .UseInMemoryDatabase(name);
        first.WithConfigureModelBuilderExtension((mb, _) => mb.Entity<Widget>());

        var second = new DbContextOptionsBuilder<FootgunContext>()
            .UseInMemoryDatabase(name);
        second.WithConfigureModelBuilderExtension((mb, _) => mb.Entity<Gadget>());

        // Realize the FIRST configuration's model. This caches it for the FootgunContext type.
        using (var ctx = new FootgunContext(first.Options))
        {
            Assert.NotNull(ctx.Model.FindEntityType(typeof(Widget)));
            Assert.Null(ctx.Model.FindEntityType(typeof(Gadget)));
        }

        // The SECOND configuration maps Gadget, but the cached model from the first configuration wins:
        // Gadget is NOT present and Widget IS — the second set of actions never ran.
        using (var ctx = new FootgunContext(second.Options))
        {
            Assert.NotNull(ctx.Model.FindEntityType(typeof(Widget)));
            Assert.Null(ctx.Model.FindEntityType(typeof(Gadget)));
        }
    }

    /// <summary>Dedicated context type so its cached model never collides with other tests.</summary>
    public class FootgunContext(DbContextOptions options) : ConfigurableContextBase(options);
}