using Microsoft.EntityFrameworkCore;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime.Tests;

/// <summary>
///     A <see cref="DbContext" /> whose only public constructor accepts
///     <see cref="DbContextOptions{TContext}" />. This exercises the factory's
///     "single options constructor" selection branch, receiving the container-backed
///     options produced by <see cref="TestContainerDesignTimeFactory{TDbContext}" />.
/// </summary>
public sealed class OptionsCtorDbContext : DbContext
{
    public OptionsCtorDbContext(DbContextOptions<OptionsCtorDbContext> options) : base(options)
    {
    }
}

/// <summary>
///     A <see cref="DbContext" /> whose only public constructor accepts the non-generic
///     <see cref="DbContextOptions" />. This exercises the second accepted shape in the
///     factory's constructor-selection logic.
/// </summary>
public sealed class NonGenericOptionsCtorDbContext : DbContext
{
    public NonGenericOptionsCtorDbContext(DbContextOptions options) : base(options)
    {
    }
}

/// <summary>
///     A parameterless <see cref="DbContext" /> that self-configures via
///     <see cref="OnConfiguring" />. This exercises the factory's fallback path
///     (<c>ActivatorUtilities.CreateInstance</c> against an empty provider). Because the
///     factory does not hand the container connection string to a self-configuring context,
///     the test publishes it through <see cref="AmbientConnectionString" /> before creation.
/// </summary>
public sealed class ParameterlessDbContext : DbContext
{
    /// <summary>
    ///     Ambient connection string consumed by <see cref="OnConfiguring" />. Set by the test
    ///     immediately before invoking the factory so the self-configured context can connect
    ///     to the same running container.
    /// </summary>
    public static string? AmbientConnectionString { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!string.IsNullOrEmpty(AmbientConnectionString))
            optionsBuilder.UseSqlServer(AmbientConnectionString);
    }
}
