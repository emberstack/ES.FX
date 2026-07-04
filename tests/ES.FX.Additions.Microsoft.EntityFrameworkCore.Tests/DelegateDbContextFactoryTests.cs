using ES.FX.Additions.Microsoft.EntityFrameworkCore.Extensions;
using ES.FX.Additions.Microsoft.EntityFrameworkCore.Factories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.Tests;

public class DelegateDbContextFactoryTests
{
    private static DbContextOptions<T> InMemoryOptions<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    [Fact]
    public void CreateDbContext_InvokesFactoryDelegate()
    {
        var invoked = 0;
        var options = InMemoryOptions<PlainDbContext>();
        var services = new ServiceCollection().BuildServiceProvider();

        var factory = new DelegateDbContextFactory<PlainDbContext>(services, _ =>
        {
            invoked++;
            return new PlainDbContext(options);
        });

        using var ctx = factory.CreateDbContext();

        Assert.Equal(1, invoked);
        Assert.NotNull(ctx);
    }

    [Fact]
    public void CreateDbContext_PassesTheServiceProviderToTheDelegate()
    {
        var options = InMemoryOptions<PlainDbContext>();
        var services = new ServiceCollection().BuildServiceProvider();
        IServiceProvider? received = null;

        var factory = new DelegateDbContextFactory<PlainDbContext>(services, sp =>
        {
            received = sp;
            return new PlainDbContext(options);
        });

        using var _ = factory.CreateDbContext();

        Assert.Same(services, received);
    }

    [Fact]
    public void CreateDbContext_CanResolveServicesFromProvider()
    {
        var options = InMemoryOptions<PlainDbContext>();
        var services = new ServiceCollection()
            .AddSingleton(options)
            .BuildServiceProvider();

        var factory = new DelegateDbContextFactory<PlainDbContext>(services,
            sp => new PlainDbContext(sp.GetRequiredService<DbContextOptions<PlainDbContext>>()));

        using var ctx = factory.CreateDbContext();

        Assert.NotNull(ctx);
    }

    [Fact]
    public void CreateDbContext_ReturnsANewInstanceEachCall()
    {
        var options = InMemoryOptions<PlainDbContext>();
        var services = new ServiceCollection().BuildServiceProvider();

        var factory = new DelegateDbContextFactory<PlainDbContext>(services,
            _ => new PlainDbContext(options));

        using var a = factory.CreateDbContext();
        using var b = factory.CreateDbContext();

        Assert.NotSame(a, b);
    }

    [Fact]
    public void CreateDbContext_PropagatesDelegateExceptions()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new DelegateDbContextFactory<PlainDbContext>(services,
            _ => throw new InvalidOperationException("boom"));

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext());
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void CreateDbContext_NullServiceProvider_Throws()
    {
        var options = InMemoryOptions<PlainDbContext>();
        var factory = new DelegateDbContextFactory<PlainDbContext>(null!,
            _ => new PlainDbContext(options));

        Assert.Throws<ArgumentNullException>(() => factory.CreateDbContext());
    }

    [Fact]
    public void CreateDbContext_NullFactory_Throws()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new DelegateDbContextFactory<PlainDbContext>(services, null!);

        Assert.Throws<ArgumentNullException>(() => factory.CreateDbContext());
    }

    [Fact]
    public void CreateDbContext_ImplementsIDbContextFactory()
    {
        var options = InMemoryOptions<PlainDbContext>();
        var services = new ServiceCollection().BuildServiceProvider();

        IDbContextFactory<PlainDbContext> factory =
            new DelegateDbContextFactory<PlainDbContext>(services, _ => new PlainDbContext(options));

        using var ctx = factory.CreateDbContext();
        Assert.NotNull(ctx);
    }

    [Fact]
    public async Task CreateDbContext_ProducedContext_IsFullyFunctional()
    {
        // End-to-end: factory produces a context whose model was built via the configure-extension,
        // and that context can persist and read data.
        var options = new DbContextOptionsBuilder<IsolatedDbContext<FactoryCrudMarker>>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Also(b => b.WithConfigureModelBuilderExtension((mb, _) => mb.Entity<Widget>()))
            .Options;

        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new DelegateDbContextFactory<IsolatedDbContext<FactoryCrudMarker>>(services,
            _ => new IsolatedDbContext<FactoryCrudMarker>(options));

        var ct = TestContext.Current.CancellationToken;
        await using (var ctx = factory.CreateDbContext())
        {
            ctx.Add(new Widget { Id = 7, Name = "seven" });
            await ctx.SaveChangesAsync(ct);
        }

        await using (var ctx = factory.CreateDbContext())
        {
            var stored = await ctx.Set<Widget>().FindAsync([7], ct);
            Assert.NotNull(stored);
            Assert.Equal("seven", stored!.Name);
        }
    }
}

internal struct FactoryCrudMarker;

internal static class BuilderFluent
{
    /// <summary>Small helper to keep the fluent chain readable when applying the configure extension.</summary>
    public static DbContextOptionsBuilder<T> Also<T>(this DbContextOptionsBuilder<T> builder,
        Action<DbContextOptionsBuilder<T>> action) where T : DbContext
    {
        action(builder);
        return builder;
    }
}