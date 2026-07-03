using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime;

/// <summary>
///     DbContext design time factory using <see cref="Testcontainers.MsSql" />
/// </summary>
/// <remarks>
///     The started container intentionally outlives <see cref="CreateDbContext" /> so the EF Core tools can connect to
///     it; cleanup is handled by Testcontainers' resource reaper (Ryuk) after the design-time process exits.
/// </remarks>
/// <typeparam name="TDbContext"> The <see cref="TDbContext" /> to create</typeparam>
public class TestContainerDesignTimeFactory<TDbContext> : IDesignTimeDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    /// <summary>
    ///     The container registry from which the SQL Server image is pulled
    /// </summary>
    public static readonly string Registry = "mcr.microsoft.com";

    /// <summary>
    ///     The SQL Server container image name
    /// </summary>
    public static readonly string Image = "mssql/server";

    /// <summary>
    ///     The SQL Server container image tag
    /// </summary>
    /// <remarks>
    ///     Exposed as <c>static readonly</c> (not <c>const</c>) so a corrected tag in a newer package version reaches
    ///     already-compiled consumers instead of being inlined at their compile time.
    /// </remarks>
    public static readonly string Tag = "2025-latest";

    private MsSqlContainer? _container;

    /// <inheritdoc />
    public TDbContext CreateDbContext(string[] args)
    {
        var builder = new MsSqlBuilder($"{Registry}/{Image}:{Tag}")
            // GetType().Name for a closed generic carries a backtick + arity suffix (e.g. "...`1") that Docker
            // rejects in a container name; strip it so the generated name is always Docker-legal.
            .WithName($"{GetType().Name.Split('`')[0]}-{Guid.NewGuid():N}");
        ConfigureMsSqlContainerBuilder(builder);
        _container = builder.Build();

        _container.StartAsync().GetAwaiter().GetResult();
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
            ConfigureDbContextOptionsBuilder(optionsBuilder);
            optionsBuilder.UseSqlServer(_container.GetConnectionString(), ConfigureSqlServerOptions);

            return CreateDbContextInstance(optionsBuilder.Options);
        }
        catch
        {
            _container.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }
    }

    /// <summary>
    ///     Creates the <typeparamref name="TDbContext" /> instance, mirroring EF Core's own
    ///     <c>DbContextFactorySource&lt;TContext&gt;</c> constructor selection without depending on EF internal APIs:
    ///     a single constructor accepting <see cref="DbContextOptions{TContext}" /> or <see cref="DbContextOptions" />
    ///     receives the configured options; any other shape (including a parameterless constructor, which self-configures
    ///     via <c>OnConfiguring</c>) is resolved from an empty service provider, matching the previous behavior.
    /// </summary>
    private static TDbContext CreateDbContextInstance(DbContextOptions<TDbContext> options)
    {
        var constructors = typeof(TDbContext).GetConstructors()
            .Where(constructor => constructor.GetParameters().Length != 0)
            .ToArray();

        if (constructors.Length == 1)
        {
            var parameters = constructors[0].GetParameters();
            if (parameters.Length == 1 &&
                (parameters[0].ParameterType == typeof(DbContextOptions<TDbContext>) ||
                 parameters[0].ParameterType == typeof(DbContextOptions)))
                return (TDbContext)constructors[0].Invoke([options]);
        }

        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return ActivatorUtilities.CreateInstance<TDbContext>(serviceProvider);
    }

    /// <summary>
    ///     Configures the <see cref="DbContextOptionsBuilder{TDbContext}" />> before the DbContext is created
    /// </summary>
    protected virtual void ConfigureDbContextOptionsBuilder(DbContextOptionsBuilder<TDbContext> builder)
    {
    }

    /// <summary>
    ///     Configures the MsSqlBuilder before the container is created
    /// </summary>
    protected virtual void ConfigureMsSqlContainerBuilder(MsSqlBuilder builder)
    {
    }

    /// <summary>
    ///     Configures the SqlServerDbContextOptionsBuilder before the DbContext is created
    /// </summary>
    protected virtual void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder builder)
    {
    }
}