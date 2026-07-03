using Microsoft.EntityFrameworkCore.Design;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime.Tests;

/// <summary>
///     Functional regression coverage for <see cref="TestContainerDesignTimeFactory{TDbContext}" />.
///     These tests start (or attempt to start) real SQL Server containers via Testcontainers and therefore
///     require Docker.
/// </summary>
/// <remarks>
///     <para>
///         KNOWN LIBRARY BUG (asserted, not worked around, per the test mandate): the factory names its
///         container <c>$"{GetType().Name}-{Guid.NewGuid():N}"</c>. For a closed generic type,
///         <see cref="System.Reflection.MemberInfo.Name" /> returns the arity-suffixed form
///         (e.g. <c>TestContainerDesignTimeFactory`1</c>) which contains a backtick — a character Docker
///         rejects in container names (<c>only [a-zA-Z0-9][a-zA-Z0-9_.-] are allowed</c>). Because the
///         factory is <em>always</em> used as a closed generic, <see cref="TestContainerDesignTimeFactory{TDbContext}.CreateDbContext" />
///         currently fails for every real usage with a <c>Docker.DotNet.DockerApiException</c> before the
///         constructor-selection logic is ever reached.
///     </para>
///     <para>
///         The tests below pin this CURRENT behavior so the suite stays green and acts as a regression guard:
///         once the library sanitizes the container name, the "throws" assertions will start failing and flag
///         the change for review (at which point they should be flipped to assert a connectable context and to
///         exercise the DbContextOptions&lt;T&gt;, DbContextOptions, and parameterless/OnConfiguring
///         constructor-selection branches end to end).
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
    public async Task CreateDbContext_WithGenericOptionsConstructor_CurrentlyFailsOnInvalidContainerName()
    {
        // Intended behavior (once the container-name bug is fixed): return a context whose single
        // DbContextOptions<TContext> constructor received the container-backed options and that can connect.
        // CURRENT behavior: the container name contains a backtick from the closed-generic type name, which
        // Docker rejects, so CreateDbContext throws before constructing the context. Asserting the real
        // behavior keeps this as a regression guard.
        var factory = new TestContainerDesignTimeFactory<OptionsCtorDbContext>();

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => Task.Run(() => factory.CreateDbContext([]), TestContext.Current.CancellationToken));

        AssertInvalidContainerNameBug(exception);
    }

    [Fact]
    public async Task CreateDbContext_WithNonGenericOptionsConstructor_CurrentlyFailsOnInvalidContainerName()
    {
        // Same known bug via a different accepted constructor shape (non-generic DbContextOptions).
        var factory = new TestContainerDesignTimeFactory<NonGenericOptionsCtorDbContext>();

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => Task.Run(() => factory.CreateDbContext([]), TestContext.Current.CancellationToken));

        AssertInvalidContainerNameBug(exception);
    }

    [Fact]
    public async Task CreateDbContext_WithParameterlessOnConfiguringContext_CurrentlyFailsOnInvalidContainerName()
    {
        // Intended behavior (once fixed): resolve the parameterless context via the ActivatorUtilities
        // fallback branch and return a self-configuring (OnConfiguring) context. We pre-publish the fixture's
        // connection string so that flip is a one-line change. CURRENT behavior: the same invalid-container-name
        // failure occurs before the fallback branch runs.
        ParameterlessDbContext.AmbientConnectionString = fixture.ConnectionString;
        try
        {
            var factory = new TestContainerDesignTimeFactory<ParameterlessDbContext>();

            var exception = await Assert.ThrowsAnyAsync<Exception>(
                () => Task.Run(() => factory.CreateDbContext([]), TestContext.Current.CancellationToken));

            AssertInvalidContainerNameBug(exception);
        }
        finally
        {
            ParameterlessDbContext.AmbientConnectionString = null;
        }
    }

    /// <summary>
    ///     Asserts the failure is the known invalid-container-name bug (backtick in the closed-generic type
    ///     name) rather than an unrelated Docker/environment error, so the guard does not silently pass on a
    ///     different failure.
    /// </summary>
    private static void AssertInvalidContainerNameBug(Exception exception)
    {
        // The Testcontainers/Docker.DotNet exception message surfaces the rejected container name and the
        // allowed-character rule. Both fragments together pin this to the arity-suffix bug.
        var message = FlattenMessages(exception);
        Assert.Contains("Invalid container name", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`1", message, StringComparison.Ordinal);
    }

    private static string FlattenMessages(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(" | ", messages);
    }
}

/// <summary>
///     xUnit collection binding the shared <see cref="SqlServerFixture" /> so the container is started once
///     for the test that needs an externally-owned reachable database (used by the intended-behavior path).
/// </summary>
[CollectionDefinition(nameof(SqlServerCollection))]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
