using ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests.Context;

[PublicAPI]
public class TestDbContextDesignTimeFactory : TestContainerDesignTimeFactory<TestDbContext>
{
    protected override void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder builder)
    {
        base.ConfigureSqlServerOptions(builder);
        builder.MigrationsAssembly(GetType().Assembly.FullName);
    }
}