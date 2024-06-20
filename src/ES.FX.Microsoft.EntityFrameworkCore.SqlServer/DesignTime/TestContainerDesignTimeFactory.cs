﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace ES.FX.Microsoft.EntityFrameworkCore.SqlServer.DesignTime;

/// <summary>
///     DbContext design time factory using MsSql Testcontainers
/// </summary>
/// <typeparam name="TDbContext"> The <see cref="TDbContext" /> to create</typeparam>
public class TestContainerDesignTimeFactory<TDbContext> : IDesignTimeDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    private MsSqlContainer? _container;

    public TDbContext CreateDbContext(string[] args)
    {
        var builder = new MsSqlBuilder().WithName($"{GetType().Name}");
        ConfigureMsSqlContainerBuilder(builder);
        _container = builder.Build();

        _container.StartAsync().Wait();
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        optionsBuilder.UseSqlServer(_container.GetConnectionString(), ConfigureSqlServerOptions);


#pragma warning disable EF1001
        var context = new DbContextFactorySource<TDbContext>().Factory(new ServiceCollection().BuildServiceProvider(),
            optionsBuilder.Options);
#pragma warning restore EF1001

        return context;
    }

    /// <summary>
    ///     Configures the SqlServerDbContextOptionsBuilder before the DbContext is created
    /// </summary>
    protected virtual void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder builder)
    {
    }

    /// <summary>
    ///     Configures the MsSqlBuilder before the container is created
    /// </summary>
    protected virtual void ConfigureMsSqlContainerBuilder(MsSqlBuilder builder)
    {
    }
}