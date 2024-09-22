using ES.FX.Microsoft.EntityFrameworkCore.Extensions;
using ES.FX.Microsoft.EntityFrameworkCore.SqlServer.DesignTime;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Playground.Shared.Data.Simple.EntityFrameworkCore.SqlServer;

[PublicAPI]
public class SimpleDbContextDesignTimeFactory : TestContainerDesignTimeFactory<SimpleDbContext>
{
    protected override void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder builder)
    {
        base.ConfigureSqlServerOptions(builder);
        builder.MigrationsAssembly(GetType().Assembly.FullName);
    }

    protected override void ConfigureDbContextOptionsBuilder(DbContextOptionsBuilder<SimpleDbContext> builder)
    {
        base.ConfigureDbContextOptionsBuilder(builder);
        builder.WithConfigureModelBuilderExtension((modelBuilder, _) =>
            modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly));
    }
}