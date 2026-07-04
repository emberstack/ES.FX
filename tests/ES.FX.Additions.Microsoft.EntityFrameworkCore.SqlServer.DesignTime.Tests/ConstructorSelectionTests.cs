using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime.Tests;

/// <summary>
///     Deterministic, Docker-free coverage for the private constructor-selection logic in
///     <see cref="TestContainerDesignTimeFactory{TDbContext}" /> (<c>CreateDbContextInstance</c>).
/// </summary>
/// <remarks>
///     <para>
///         These tests target the shipped private <c>CreateDbContextInstance</c> directly (Docker-free): the
///         constructor-selection branches are pure reflection over the target <see cref="DbContext" /> type, so
///         they are fully deterministic without a container. We assert the observable outcome of every branch:
///         the single generic-options constructor, the single non-generic-options constructor, and the
///         <c>ActivatorUtilities</c> fallback (parameterless, multi-constructor, and unsatisfiable shapes). The
///         end-to-end public path (with a live container) is covered by <c>TestContainerDesignTimeFactoryTests</c>.
///     </para>
///     <para>
///         Options are built with EF Core's in-memory provider so no database or container is required. The
///         point of these tests is the constructor selection and options wiring, not provider behavior.
///     </para>
/// </remarks>
public sealed class ConstructorSelectionTests
{
    /// <summary>
    ///     Invokes the private static <c>CreateDbContextInstance(DbContextOptions&lt;TContext&gt;)</c> for the
    ///     given closed generic factory type, unwrapping reflection's <see cref="TargetInvocationException" />
    ///     so callers see the real exception.
    /// </summary>
    private static TContext InvokeCreateDbContextInstance<TContext>(DbContextOptions<TContext> options)
        where TContext : DbContext
    {
        var method = typeof(TestContainerDesignTimeFactory<TContext>)
            .GetMethod("CreateDbContextInstance", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        try
        {
            return (TContext)method.Invoke(null, [options])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static DbContextOptions<TContext> InMemoryOptions<TContext>() where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    [Fact]
    public void SingleGenericOptionsConstructor_ReceivesTheConfiguredOptions()
    {
        var options = InMemoryOptions<OptionsCtorDbContext>();

        using var context = InvokeCreateDbContextInstance(options);

        // The generic DbContextOptions<TContext> constructor branch was taken: the returned context is the
        // correct type and EF surfaces the very options instance we passed in (proving options were wired,
        // not that a fresh unconfigured context was produced).
        Assert.IsType<OptionsCtorDbContext>(context);
        // The exact options instance we passed reached the constructor: EF exposes the constructor-supplied
        // options via IDbContextOptions (which DbContextOptions<T> implements), so reference equality proves
        // the generic-options branch invoked the ctor with our object rather than building a fresh one.
        Assert.Same(options, context.GetService<IDbContextOptions>());
        Assert.True(context.Database.IsInMemory());
    }

    [Fact]
    public void SingleNonGenericOptionsConstructor_ReceivesTheConfiguredOptions()
    {
        var options = InMemoryOptions<NonGenericOptionsCtorDbContext>();

        using var context = InvokeCreateDbContextInstance(options);

        // The non-generic DbContextOptions constructor branch was taken and the provider took effect.
        Assert.IsType<NonGenericOptionsCtorDbContext>(context);
        Assert.True(context.Database.IsInMemory());
    }

    [Fact]
    public void ParameterlessOnConfiguringContext_FallsBackToActivatorUtilities_AndSelfConfigures()
    {
        // A parameterless context has no accepted options constructor, so the fast path is skipped and the
        // ActivatorUtilities fallback constructs it from an empty provider. The passed options are ignored;
        // the context configures itself via OnConfiguring.
        ParameterlessDbContext.AmbientConnectionString =
            "Server=(local);Database=IgnoredByThisTest;Trusted_Connection=True;TrustServerCertificate=True";
        try
        {
            var options = InMemoryOptions<ParameterlessDbContext>();

            using var context = InvokeCreateDbContextInstance(options);

            Assert.IsType<ParameterlessDbContext>(context);
            // OnConfiguring wired the SQL Server provider from the ambient string — proving the fallback
            // produced a self-configuring context rather than one using the passed in-memory options.
            Assert.True(context.Database.IsSqlServer());
        }
        finally
        {
            ParameterlessDbContext.AmbientConnectionString = null;
        }
    }

    [Fact]
    public void BareParameterlessContext_FallsBackToActivatorUtilities()
    {
        // No options-accepting constructor and no self-configuration: ActivatorUtilities still builds it from
        // the empty provider. The returned context is unconfigured (no provider) but constructed successfully.
        var options = InMemoryOptions<BareParameterlessDbContext>();

        using var context = InvokeCreateDbContextInstance(options);

        Assert.IsType<BareParameterlessDbContext>(context);
    }

    [Fact]
    public void MultipleConstructors_SkipFastPath_AndFallBackToActivatorUtilities()
    {
        // constructors.Length != 1 (after filtering out the parameterless ctor there is still exactly one
        // options ctor, but ActivatorUtilities picks the greediest constructor it can satisfy from an empty
        // provider — the parameterless one). Selection must not throw and must produce the right type.
        var options = InMemoryOptions<MultiCtorDbContext>();

        using var context = InvokeCreateDbContextInstance(options);

        Assert.IsType<MultiCtorDbContext>(context);
    }

    [Fact]
    public void SingleOptionsPlusExtraParamConstructor_SkipsFastPath_AndFallbackThrows()
    {
        // The single constructor takes (DbContextOptions<T>, string). Its parameter count is 2, so the fast
        // path guard (parameters.Length == 1) is false. The fallback then asks ActivatorUtilities to build it
        // from an EMPTY provider, which cannot supply the extra string parameter -> it throws. Pinning the
        // current behavior for this unsatisfiable shape.
        var options = InMemoryOptions<OptionsPlusExtraCtorDbContext>();

        Assert.ThrowsAny<Exception>(() => InvokeCreateDbContextInstance(options));
    }
}