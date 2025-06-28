using ES.FX.TransactionalOutbox.EntityFrameworkCore.Extensions;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;

[PublicAPI]
public class OutboxTestDbContext(DbContextOptions<OutboxTestDbContext> options) : DbContext(options), IOutboxDbContext
{
    public DbSet<TestOrder> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
        });

        modelBuilder.AddOutboxEntities();
        base.OnModelCreating(modelBuilder);
    }
}