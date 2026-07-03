using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;
using Testcontainers.MariaDb;
using Xunit;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.MySql.Tests;

/// <summary>
///     Real-service (MariaDB container) tests for behaviours of
///     <see cref="MySqlOutboxProvider{TDbContext}.GetNextExclusiveOutboxWithoutDelay" /> that require
///     the raw <c>FOR UPDATE SKIP LOCKED</c> SQL to actually execute against a MySQL-compatible engine:
///     the null-result path (empty / all-locked table) and the schema-qualified table-name branch.
/// </summary>
public class MySqlOutboxProviderContainerTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private MariaDbContainer _mariaDbContainer = null!;

    public async ValueTask InitializeAsync()
    {
        _mariaDbContainer = new MariaDbBuilder("mariadb:latest").Build();
        await _mariaDbContainer.StartAsync(TestContext.Current.CancellationToken);
        _connectionString = _mariaDbContainer.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        await _mariaDbContainer.DisposeAsync();
    }

    private OutboxTestDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString),
                o => o.MigrationsAssembly(typeof(MySqlOutboxProviderContainerTests).Assembly.FullName));
        builder.UseOutbox();
        return new OutboxTestDbContext(builder.Options);
    }

    // ---- Gap 1: null-result path -------------------------------------------------------------

    [Fact]
    public async Task GetNextExclusiveOutboxWithoutDelay_Returns_Null_When_Table_Empty()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Guarantee an empty outbox table for this assertion.
        await context.Set<OutboxMessage>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await context.Set<Outbox>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        var provider = new MySqlOutboxProvider<OutboxTestDbContext>();

        await using var transaction =
            await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var result = await provider.GetNextExclusiveOutboxWithoutDelay(
            context, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetNextExclusiveOutboxWithoutDelay_Returns_Null_When_All_Rows_Locked()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await context.Set<OutboxMessage>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await context.Set<Outbox>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        // The single eligible row is pre-locked (Lock IS NOT NULL), so the WHERE clause excludes it.
        context.AddOutboxMessage(new TestOrder { OrderNumber = "LOCKED-001", Amount = 10m });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var only = await context.Set<Outbox>().SingleAsync(TestContext.Current.CancellationToken);
        only.Lock = Guid.NewGuid();
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new MySqlOutboxProvider<OutboxTestDbContext>();

        await using var transaction =
            await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var result = await provider.GetNextExclusiveOutboxWithoutDelay(
            context, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetNextExclusiveOutboxWithoutDelay_Returns_Null_When_Only_Row_Is_Delivery_Delayed()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await context.Set<OutboxMessage>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await context.Set<Outbox>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        context.AddOutboxMessage(new TestOrder { OrderNumber = "DELAYED-001", Amount = 20m });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Push the delivery window into the future: DeliveryDelayedUntil > now, so it is not eligible.
        var only = await context.Set<Outbox>().SingleAsync(TestContext.Current.CancellationToken);
        only.DeliveryDelayedUntil = DateTimeOffset.UtcNow.AddHours(1);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var provider = new MySqlOutboxProvider<OutboxTestDbContext>();

        await using var transaction =
            await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var result = await provider.GetNextExclusiveOutboxWithoutDelay(
            context, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // ---- Gap 3: schema-qualified table-name branch -------------------------------------------

    /// <summary>
    ///     Context that maps the outbox entities to an explicit (non-default) schema so the provider takes
    ///     the <c>`schema`.`table`</c> branch of the table-name builder. In MySQL/MariaDB a schema is a
    ///     database, so the schema is set to the connection's own database name.
    /// </summary>
    /// <remarks>
    ///     The Pomelo/Microting MySQL provider refuses to emit schema-qualified DDL
    ///     (<c>EnsureCreated</c>/migrations throw "MySQL does not support the EF Core concept of
    ///     schemas"). The tables are therefore materialised by a separate schema-less context pointed at
    ///     the same database, and this context is only ever used to drive the provider's runtime query -
    ///     never to create the schema. Because the schema name equals the connection's database, the
    ///     provider's <c>`schema`.`__Outboxes`</c> SQL resolves against the real, already-created table.
    /// </remarks>
    private sealed class SchemaQualifiedOutboxDbContext(
        DbContextOptions<SchemaQualifiedOutboxDbContext> options,
        string schema) : DbContext(options)
    {
        public DbSet<TestOrder> Orders { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
            });

            // AddOutbox applies the full outbox mapping (keys, indexes, row version) to the default
            // (null) schema; re-target the tables to the explicit schema so the provider takes the
            // `schema`.`table` branch of the table-name builder.
            modelBuilder.AddOutbox();
            modelBuilder.Entity<Outbox>().ToTable("__Outboxes", schema);
            modelBuilder.Entity<OutboxMessage>().ToTable("__OutboxMessages", schema);

            base.OnModelCreating(modelBuilder);
        }
    }

    private static string GetDatabaseName(string connectionString) =>
        new MySqlConnectionStringBuilder(connectionString).Database;

    [Fact]
    public async Task GetNextExclusiveOutboxWithoutDelay_Uses_SchemaQualified_Table_And_Returns_Row()
    {
        // The connection's database doubles as the "schema". Create the tables and seed a row via a
        // schema-less context (the only way the Pomelo provider will emit the DDL).
        var schema = GetDatabaseName(_connectionString);

        await using (var seedContext = CreateContext())
        {
            await seedContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            await seedContext.Set<OutboxMessage>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await seedContext.Set<Outbox>().ExecuteDeleteAsync(TestContext.Current.CancellationToken);

            seedContext.AddOutboxMessage(new TestOrder { OrderNumber = "SCHEMA-001", Amount = 30m });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Now drive the provider through a context that maps the Outbox to the explicit schema, forcing
        // the `schema`.`__Outboxes` branch. Do NOT call EnsureCreated here (the provider would throw on
        // schema DDL) - the table already exists from the seed context.
        var builder = new DbContextOptionsBuilder<SchemaQualifiedOutboxDbContext>()
            .UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString),
                o => o.MigrationsAssembly(typeof(MySqlOutboxProviderContainerTests).Assembly.FullName));
        builder.UseOutbox();

        await using var context = new SchemaQualifiedOutboxDbContext(builder.Options, schema);

        // Sanity check: the entity really is mapped to the explicit schema, so the provider will build
        // the `schema`.`table` form (not the bare `table` form).
        var entityType = context.Model.FindEntityType(typeof(Outbox));
        Assert.NotNull(entityType);
        Assert.Equal(schema, entityType.GetSchema());

        var provider = new MySqlOutboxProvider<SchemaQualifiedOutboxDbContext>();

        await using var transaction =
            await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var result = await provider.GetNextExclusiveOutboxWithoutDelay(
            context, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // The schema-qualified SQL executed successfully against the real table and returned the row -
        // proving the `schema`.`table` quoting branch produces valid, resolvable SQL.
        Assert.NotNull(result);
    }
}
