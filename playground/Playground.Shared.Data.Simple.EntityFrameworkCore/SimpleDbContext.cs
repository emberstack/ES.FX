using ES.FX.Additions.Microsoft.EntityFrameworkCore.Extensions;
using ES.FX.TransactionalOutbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore.Entities;

namespace Playground.Shared.Data.Simple.EntityFrameworkCore;

public class SimpleDbContext(
    DbContextOptions<SimpleDbContext> dbContextOptions) :
    DbContext(dbContextOptions)
{
    public required DbSet<SimpleUser> SimpleUsers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseOutbox();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
        modelBuilder.ConfigureFromExtension(dbContextOptions);
    }
}