using ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer.Tests.Context;

[PublicAPI]
public class OutboxTestDbContextDesignTimeFactory : TestContainerDesignTimeFactory<OutboxTestDbContext>
{
    protected override void ConfigureDbContextOptionsBuilder(
        DbContextOptionsBuilder<OutboxTestDbContext> optionsBuilder)
    {
        base.ConfigureDbContextOptionsBuilder(optionsBuilder);
        optionsBuilder.UseOutbox();
    }

    protected override void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder builder)
    {
        base.ConfigureSqlServerOptions(builder);
        builder.MigrationsAssembly(GetType().Assembly.FullName);
    }
}