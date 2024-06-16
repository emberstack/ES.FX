using JetBrains.Annotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Playground.Shared.Data.Simple.EntityFrameworkCore.SqlServer;

[PublicAPI]
public class SimpleDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SimpleDbContext>
{
    public SimpleDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SimpleDbContext>();
        var sqlBuilder = new SqlConnectionStringBuilder
        {
            DataSource = "(local)",
            UserID = "sa",
            Password = "SuperPass#",
            InitialCatalog = $"{nameof(SimpleDbContext)}_Design",
            TrustServerCertificate = true
        };
        optionsBuilder.UseSqlServer(sqlBuilder.ConnectionString,
            sqlServerDbContextOptionsBuilder =>
            {
                sqlServerDbContextOptionsBuilder.MigrationsAssembly(typeof(SimpleDbContextDesignTimeFactory).Assembly
                    .FullName);
            });

        return new SimpleDbContext(optionsBuilder.Options);
    }
}