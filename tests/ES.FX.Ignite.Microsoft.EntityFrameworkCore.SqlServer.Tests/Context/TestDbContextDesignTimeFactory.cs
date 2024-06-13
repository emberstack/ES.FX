using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using JetBrains.Annotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests.Context;

[PublicAPI]
public class TestDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TestDbContext>
{
    public TestDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        var sqlBuilder = new SqlConnectionStringBuilder
        {
            DataSource = "(local)",
            UserID = "sa",
            Password = "SuperPass#",
            InitialCatalog = $"{nameof(TestDbContext)}_Design",
            TrustServerCertificate = true
        };
        optionsBuilder.UseSqlServer(sqlBuilder.ConnectionString,
            sqlServerDbContextOptionsBuilder =>
            {
                sqlServerDbContextOptionsBuilder.MigrationsAssembly(typeof(TestDbContextDesignTimeFactory).Assembly
                    .FullName);
            });

        return new TestDbContext(optionsBuilder.Options);
    }
}