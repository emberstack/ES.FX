using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime.Tests;

/// <summary>
///     Functional regression coverage for <see cref="TestContainerDesignTimeFactory{TDbContext}" />.
///     These tests start real SQL Server containers via Testcontainers (through the shared
///     <see cref="SqlServerFixture" />) and therefore require Docker.
/// </summary>
/// <remarks>
///     <para>
///         The public <see cref="TestContainerDesignTimeFactory{TDbContext}.CreateDbContext" /> is driven end to
///         end: the factory strips the backtick + arity suffix that <c>GetType().Name</c> produces for a closed
///         generic (source line 45), so the container name is Docker-legal, the container starts, and a
///         connectable context is returned.
///         <see cref="CreateDbContext_ClosedGenericFactory_StartsContainerAndReturnsConnectableContext" /> guards
///         that (a reintroduced backtick or broken options wiring makes it fail).
///     </para>
///     <para>
///         The constructor-selection + options wiring core (shipped private <c>CreateDbContextInstance</c>) is
///         also driven here with options carrying a REAL running container's connection string, asserting the
///         returned context connects and round-trips a query for each accepted constructor shape.
///     </para>
/// </remarks>
[Collection(nameof(SqlServerCollection))]
public sealed class TestContainerDesignTimeFactoryTests(SqlServerFixture fixture)
{
    [Fact]
    public void Factory_ImplementsDesignTimeDbContextFactory()
    {
        // Guards the public contract EF Core tools depend on: the type must be discoverable as an
        // IDesignTimeDbContextFactory<TDbContext>.
        Assert.IsAssignableFrom<IDesignTimeDbContextFactory<OptionsCtorDbContext>>(
            new TestContainerDesignTimeFactory<OptionsCtorDbContext>());
    }

    [Fact]
    public void ImageCoordinates_AreExposedWithExpectedValues()
    {
        // These are part of the public surface and are asserted as static readonly (not const),
        // per the library's documented intent that a corrected tag reaches compiled consumers.
        Assert.Equal("mcr.microsoft.com", TestContainerDesignTimeFactory<OptionsCtorDbContext>.Registry);
        Assert.Equal("mssql/server", TestContainerDesignTimeFactory<OptionsCtorDbContext>.Image);
        Assert.Equal("2025-latest", TestContainerDesignTimeFactory<OptionsCtorDbContext>.Tag);

        // Static members of an open generic are per-closed-type; assert another closed type sees the same
        // coordinate values so the exposed contract is stable across instantiations.
        Assert.Equal(
            TestContainerDesignTimeFactory<OptionsCtorDbContext>.Registry,
            TestContainerDesignTimeFactory<ParameterlessDbContext>.Registry);
        Assert.Equal(
            TestContainerDesignTimeFactory<OptionsCtorDbContext>.Image,
            TestContainerDesignTimeFactory<ParameterlessDbContext>.Image);
        Assert.Equal(
            TestContainerDesignTimeFactory<OptionsCtorDbContext>.Tag,
            TestContainerDesignTimeFactory<ParameterlessDbContext>.Tag);
    }

    [Fact]
    public async Task CreateDbContextInstance_GenericOptionsConstructor_ProducesLiveConnectableContext()
    {
        // Drives the shipped constructor-selection + options wiring against a REAL SQL Server container:
        // the single DbContextOptions<TContext> constructor must receive the container-backed options and the
        // returned context must actually connect and round trip. If the wrong branch were taken, or the options
        // not carried through, the context could not reach the live database.
        var options = SqlServerOptions<OptionsCtorDbContext>(fixture.ConnectionString);

        await using var context = InvokeCreateDbContextInstance(options);

        Assert.IsType<OptionsCtorDbContext>(context);
        Assert.True(context.Database.IsSqlServer());
        await AssertCanRoundTripAsync(context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateDbContextInstance_NonGenericOptionsConstructor_ProducesLiveConnectableContext()
    {
        // Same live-connectivity guarantee via the non-generic DbContextOptions constructor branch.
        var options = SqlServerOptions<NonGenericOptionsCtorDbContext>(fixture.ConnectionString);

        await using var context = InvokeCreateDbContextInstance(options);

        Assert.IsType<NonGenericOptionsCtorDbContext>(context);
        Assert.True(context.Database.IsSqlServer());
        await AssertCanRoundTripAsync(context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateDbContextInstance_ParameterlessOnConfiguringContext_ProducesLiveSelfConfiguredContext()
    {
        // The ActivatorUtilities fallback branch: a parameterless context ignores the passed options and
        // self-configures via OnConfiguring from the ambient connection string (pointed at the live container).
        // The returned context must connect and round trip, proving the fallback yields a usable instance.
        ParameterlessDbContext.AmbientConnectionString = fixture.ConnectionString;
        try
        {
            // The passed options intentionally target a DIFFERENT (in-memory) provider so that a context which
            // actually connects to SQL Server can only have done so via its own OnConfiguring, not by reusing
            // these options.
            var ignoredOptions = new DbContextOptionsBuilder<ParameterlessDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;

            await using var context = InvokeCreateDbContextInstance(ignoredOptions);

            Assert.IsType<ParameterlessDbContext>(context);
            Assert.True(context.Database.IsSqlServer());
            await AssertCanRoundTripAsync(context, TestContext.Current.CancellationToken);
        }
        finally
        {
            ParameterlessDbContext.AmbientConnectionString = null;
        }
    }

    [Fact]
    public async Task CreateDbContext_ClosedGenericFactory_StartsContainerAndReturnsConnectableContext()
    {
        // End-to-end through the PUBLIC API on a raw closed-generic factory. The factory strips the backtick +
        // arity suffix that GetType().Name produces for "TestContainerDesignTimeFactory`1" (source line 45), so
        // the container name is Docker-legal: the container starts and a usable, connectable OptionsCtorDbContext
        // is returned. Reintroducing the backtick, or breaking the options wiring, makes this fail.
        var factory = new TestContainerDesignTimeFactory<OptionsCtorDbContext>();

        await using var context = factory.CreateDbContext([]);

        Assert.IsType<OptionsCtorDbContext>(context);
        Assert.True(context.Database.IsSqlServer());
        await AssertCanRoundTripAsync(context, TestContext.Current.CancellationToken);
    }

    /// <summary>
    ///     Invokes the shipped private static <c>CreateDbContextInstance(DbContextOptions&lt;TContext&gt;)</c>
    ///     for the given closed generic factory type, unwrapping reflection's
    ///     <see cref="TargetInvocationException" /> so callers see the real exception.
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

    private static DbContextOptions<TContext> SqlServerOptions<TContext>(string connectionString)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseSqlServer(connectionString).Options;

    /// <summary>
    ///     Proves the returned context can actually reach its database: opens the connection and executes a
    ///     trivial scalar query, asserting the round-tripped value. A context that was not wired to the live
    ///     container (wrong provider, missing connection string) cannot satisfy this.
    /// </summary>
    private static async Task AssertCanRoundTripAsync(DbContext context, CancellationToken cancellationToken)
    {
        Assert.True(await context.Database.CanConnectAsync(cancellationToken));

        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT 42";
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            Assert.Equal(42, Convert.ToInt32(scalar));
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}

/// <summary>
///     xUnit collection binding the shared <see cref="SqlServerFixture" /> so the container is started once
///     for the live-database end-to-end tests above.
/// </summary>
[CollectionDefinition(nameof(SqlServerCollection))]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;