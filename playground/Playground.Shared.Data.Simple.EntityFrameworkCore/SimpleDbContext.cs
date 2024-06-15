using Microsoft.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore.Entities;

namespace Playground.Shared.Data.Simple.EntityFrameworkCore;

public class SimpleDbContext(
    DbContextOptions<SimpleDbContext> dbContextOptions) :
    DbContext(dbContextOptions)
{
    public DbSet<SimpleUser> SimpleUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfigurationsFromAssembly(typeof(SimpleDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}