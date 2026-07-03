using ES.FX.Additions.Microsoft.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.Tests;

public class BuilderExtensionsTests
{
    private static DbContextOptions<TContext> BuildOptions<TContext>(
        Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        // Unique DB name so InMemory data never bleeds across tests.
        builder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        configure?.Invoke(builder);
        return builder.Options;
    }

    [Fact]
    public void WithConfigureModelBuilderExtension_RegistersExtension()
    {
        var builder = new DbContextOptionsBuilder();
        builder.WithConfigureModelBuilderExtension((_, _) => { });

        var extension = builder.Options.FindExtension<ModelBuilderConfigureExtension>();
        Assert.NotNull(extension);
        Assert.Single(extension.ConfigureActions);
    }

    [Fact]
    public void WithConfigureModelBuilderExtension_NoExtensionRegistered_WhenNeverCalled()
    {
        var builder = new DbContextOptionsBuilder();
        Assert.Null(builder.Options.FindExtension<ModelBuilderConfigureExtension>());
    }

    [Fact]
    public void WithConfigureModelBuilderExtension_MultipleActionsInSingleCall_AllStored()
    {
        var builder = new DbContextOptionsBuilder();
        builder.WithConfigureModelBuilderExtension((_, _) => { }, (_, _) => { }, (_, _) => { });

        var extension = builder.Options.FindExtension<ModelBuilderConfigureExtension>();
        Assert.NotNull(extension);
        Assert.Equal(3, extension.ConfigureActions.Count);
    }

    [Fact]
    public void WithConfigureModelBuilderExtension_SubsequentCalls_AppendActions()
    {
        var builder = new DbContextOptionsBuilder();
        builder.WithConfigureModelBuilderExtension((_, _) => { });
        builder.WithConfigureModelBuilderExtension((_, _) => { }, (_, _) => { });

        var extension = builder.Options.FindExtension<ModelBuilderConfigureExtension>();
        Assert.NotNull(extension);
        Assert.Equal(3, extension.ConfigureActions.Count);
    }

    [Fact]
    public void WithConfigureModelBuilderExtension_SubsequentCalls_PreserveOrder()
    {
        var order = new List<string>();
        var builder = new DbContextOptionsBuilder();
        builder.WithConfigureModelBuilderExtension((_, _) => order.Add("a"));
        builder.WithConfigureModelBuilderExtension((_, _) => order.Add("b"), (_, _) => order.Add("c"));

        var extension = builder.Options.FindExtension<ModelBuilderConfigureExtension>();
        Assert.NotNull(extension);
        // Invoke via the extension directly (no ModelBuilder needed for these no-op-ish delegates).
        foreach (var action in extension.ConfigureActions) action.Invoke(null!, null!);

        Assert.Equal(["a", "b", "c"], order);
    }

    [Fact]
    public void WithConfigureModelBuilderExtension_EmptyParams_RegistersExtensionWithNoActions()
    {
        var builder = new DbContextOptionsBuilder();
        builder.WithConfigureModelBuilderExtension();

        var extension = builder.Options.FindExtension<ModelBuilderConfigureExtension>();
        Assert.NotNull(extension);
        Assert.Empty(extension.ConfigureActions);
    }

    [Fact]
    public void WithConfigureModelBuilderExtension_NullBuilder_Throws()
    {
        DbContextOptionsBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithConfigureModelBuilderExtension((_, _) => { }));
    }

    [Fact]
    public void WithConfigureModelBuilderExtension_NullActionsArray_Throws()
    {
        var builder = new DbContextOptionsBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            builder.WithConfigureModelBuilderExtension(null!));
    }

    [Fact]
    public void ConfigureFromExtension_NullModelBuilder_Throws()
    {
        var options = new DbContextOptionsBuilder().Options;
        Assert.Throws<ArgumentNullException>(() =>
            BuilderExtensions.ConfigureFromExtension(null!, options));
    }

    [Fact]
    public void ConfigureFromExtension_NullOptions_Throws()
    {
        // ModelBuilder has a public parameterless constructor in EF Core; use it to satisfy the
        // first (non-null) guard so the options-null guard is what actually fires.
        var mb = new ModelBuilder();
        Assert.Throws<ArgumentNullException>(() =>
            BuilderExtensions.ConfigureFromExtension(mb, null!));
    }

    [Fact]
    public void ConfigureFromExtension_NoExtensionPresent_IsNoOp()
    {
        var options = BuildOptions<PlainDbContext>();
        using var ctx = new PlainDbContext(options);

        // Model builds fine; no extension means nothing extra mapped.
        Assert.Null(ctx.Model.FindEntityType(typeof(Widget)));
    }

    // Per-test marker types so each context below gets its own model cache entry.
    private struct M1;

    private struct M2;

    private struct M3;

    private struct M4;

    private struct M5;

    [Fact]
    public void ConfigureFromExtension_SingleAction_RunsAndMapsEntity()
    {
        var options = BuildOptions<IsolatedDbContext<M1>>(b =>
            b.WithConfigureModelBuilderExtension((mb, _) => mb.Entity<Widget>()));

        using var ctx = new IsolatedDbContext<M1>(options);

        // The Widget entity is only in the model because the registered action ran.
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Widget)));
    }

    [Fact]
    public void ConfigureFromExtension_ReceivesTheOptionsInstance()
    {
        DbContextOptions? received = null;
        var options = BuildOptions<IsolatedDbContext<M2>>(b =>
            b.WithConfigureModelBuilderExtension((mb, opt) =>
            {
                received = opt;
                mb.Entity<Widget>();
            }));

        using var ctx = new IsolatedDbContext<M2>(options);
        _ = ctx.Model; // trigger OnModelCreating

        Assert.NotNull(received);
        Assert.NotNull(received!.FindExtension<ModelBuilderConfigureExtension>());
    }

    [Fact]
    public void ConfigureFromExtension_MultipleActions_AllRun()
    {
        var options = BuildOptions<IsolatedDbContext<M3>>(b =>
            b.WithConfigureModelBuilderExtension(
                (mb, _) => mb.Entity<Widget>(),
                (mb, _) => mb.Entity<Gadget>()));

        using var ctx = new IsolatedDbContext<M3>(options);

        Assert.NotNull(ctx.Model.FindEntityType(typeof(Widget)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Gadget)));
    }

    [Fact]
    public void ConfigureFromExtension_ActionCanCustomizeMapping_ReflectedInModel()
    {
        // Use provider-agnostic customizations (HasMaxLength, HasKey) so this holds on the InMemory
        // provider, which does not pull in EF Core.Relational (no ToTable/GetTableName).
        var options = BuildOptions<IsolatedDbContext<M4>>(b =>
            b.WithConfigureModelBuilderExtension((mb, _) =>
                mb.Entity<Widget>(e =>
                {
                    e.Property(w => w.Name).HasMaxLength(42);
                    e.HasKey(w => w.Id);
                })));

        using var ctx = new IsolatedDbContext<M4>(options);
        var entity = ctx.Model.FindEntityType(typeof(Widget));

        Assert.NotNull(entity);
        var nameProp = entity!.FindProperty(nameof(Widget.Name));
        Assert.NotNull(nameProp);
        Assert.Equal(42, nameProp!.GetMaxLength());
        // Primary key configured by the action is reflected in the model.
        Assert.NotNull(entity.FindPrimaryKey());
        Assert.Equal(nameof(Widget.Id), entity.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public async Task ConfigureFromExtension_MappedEntity_IsUsableForCrud()
    {
        var options = BuildOptions<IsolatedDbContext<M5>>(b =>
            b.WithConfigureModelBuilderExtension((mb, _) => mb.Entity<Widget>()));

        var ct = TestContext.Current.CancellationToken;
        await using (var ctx = new IsolatedDbContext<M5>(options))
        {
            ctx.Add(new Widget { Id = 1, Name = "alpha" });
            await ctx.SaveChangesAsync(ct);
        }

        await using (var ctx = new IsolatedDbContext<M5>(options))
        {
            var stored = await ctx.Set<Widget>().FindAsync([1], ct);
            Assert.NotNull(stored);
            Assert.Equal("alpha", stored!.Name);
        }
    }

    [Fact]
    public void WithConfigureModelBuilderExtension_ThenBuildContext_DoesNotThrow_WhenNoContextConsumesIt()
    {
        // Registering the extension on a context that never replays it must not break model creation.
        var options = BuildOptions<PlainDbContext>(b =>
            b.WithConfigureModelBuilderExtension((mb, _) => mb.Entity<Widget>()));

        using var ctx = new PlainDbContext(options);
        // PlainDbContext ignores the extension entirely, so Widget is not mapped.
        Assert.Null(ctx.Model.FindEntityType(typeof(Widget)));
    }
}
