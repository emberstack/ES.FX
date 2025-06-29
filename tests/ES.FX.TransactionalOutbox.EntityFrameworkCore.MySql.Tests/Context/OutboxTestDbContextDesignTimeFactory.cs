using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.MySql.Tests.Context;

public class OutboxTestDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OutboxTestDbContext>
{
    public OutboxTestDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseMySql("Server=localhost;Port=3306;Database=OutboxTestDb;Uid=root;Pwd=root;",
                ServerVersion.Create(10, 11, 0, ServerType.MariaDb),
                o => o.MigrationsAssembly(typeof(OutboxTestDbContextDesignTimeFactory).Assembly.FullName));

        builder.UseOutbox();

        return new OutboxTestDbContext(builder.Options);
    }
}