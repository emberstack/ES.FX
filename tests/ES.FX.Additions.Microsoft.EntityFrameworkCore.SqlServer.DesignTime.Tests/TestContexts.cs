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

/// <summary>
///     A parameterless <see cref="DbContext" /> that does NOT self-configure. Used to exercise the factory's
///     fallback branch (<c>ActivatorUtilities.CreateInstance</c> against an empty provider) deterministically:
///     the branch is selected because the type has no options-accepting constructor after the length filter,
///     and <c>ActivatorUtilities</c> can satisfy the parameterless constructor without any registered services.
/// </summary>
public sealed class BareParameterlessDbContext : DbContext;

/// <summary>
///     A <see cref="DbContext" /> that exposes two constructors, neither of which is the single accepted
///     options shape the fast path looks for (<c>constructors.Length == 1</c> is false). This forces the
///     fallback branch, where <c>ActivatorUtilities</c> selects the greediest satisfiable constructor.
/// </summary>
public sealed class MultiCtorDbContext : DbContext
{
    public MultiCtorDbContext()
    {
    }

    public MultiCtorDbContext(DbContextOptions<MultiCtorDbContext> options) : base(options)
    {
    }
}

/// <summary>
///     A <see cref="DbContext" /> whose single constructor takes <see cref="DbContextOptions{TContext}" />
///     PLUS an extra parameter. This is a single constructor, but its parameter count is not 1, so it fails
///     the fast-path guard and routes to the fallback. <c>ActivatorUtilities</c> against an empty provider
///     cannot satisfy the extra <see cref="string" /> parameter, so construction throws.
/// </summary>
public sealed class OptionsPlusExtraCtorDbContext : DbContext
{
    public OptionsPlusExtraCtorDbContext(DbContextOptions<OptionsPlusExtraCtorDbContext> options, string extra)
        : base(options)
    {
        Extra = extra;
    }

    public string Extra { get; }
}
