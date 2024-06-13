using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests.Context;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using ES.FX.Shared.SqlServer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests;

public class SqlServerDbContextConnectTests(SqlServerContainerFixture sqlServerFixture) : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task AddSqlServerDbContext_CanConnect()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerDbContext<TestDbContext>(configureOptions: options =>
            {
                options.ConnectionString = sqlServerFixture.GetConnectionString();
            },
            configureSqlServerDbContextOptionsBuilder: sqlServerDbContextOptionsBuilder =>
            {
                sqlServerDbContextOptionsBuilder.MigrationsAssembly(
                    typeof(TestDbContextDesignTimeFactory).Assembly.FullName);
            });

        var app = builder.Build();


        var context = app.Services.GetRequiredService<TestDbContext>();
        Assert.True(await context.Database.CanConnectAsync());

        await context.Database.MigrateAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendingMigrations);
    }
}