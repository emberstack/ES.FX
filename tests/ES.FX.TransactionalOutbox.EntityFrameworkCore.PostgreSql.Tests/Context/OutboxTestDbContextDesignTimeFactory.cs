using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.PostgreSql.Tests.Context;

public class OutboxTestDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OutboxTestDbContext>
{
    public OutboxTestDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseNpgsql("Host=localhost;Database=OutboxTestDb;Username=postgres;Password=postgres",
                o => o.MigrationsAssembly(typeof(OutboxTestDbContextDesignTimeFactory).Assembly.FullName));

        builder.UseOutbox();

        return new OutboxTestDbContext(builder.Options);
    }
}