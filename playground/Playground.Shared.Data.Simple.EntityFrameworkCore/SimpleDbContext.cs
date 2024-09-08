using ES.FX.Microsoft.EntityFrameworkCore.Extensions;
using ES.FX.TransactionalOutbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore.Entities;

namespace Playground.Shared.Data.Simple.EntityFrameworkCore;

public class SimpleDbContext(
    DbContextOptions<SimpleDbContext> dbContextOptions) :
    DbContext(dbContextOptions), IOutboxDbContext
{
    public DbSet<SimpleUser> SimpleUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutboxEntities();
        modelBuilder.ApplyConfigurationsFromAssembliesExtension(dbContextOptions);
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddOutboxBehavior();
        base.OnConfiguring(optionsBuilder);
    }
}