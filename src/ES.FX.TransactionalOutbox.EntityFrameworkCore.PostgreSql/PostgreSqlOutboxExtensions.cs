using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.PostgreSql;

/// <summary>
///     Extension methods for configuring PostgreSQL-specific outbox functionality.
/// </summary>
[PublicAPI]
public static class PostgreSqlOutboxExtensions
{
    /// <summary>
    ///     Registers PostgreSQL optimized services for the outbox delivery
    /// </summary>
    /// <typeparam name="TDbContext"><see cref="TDbContext" /> context for the options </typeparam>
    /// <param name="options">The <see cref="OutboxDeliveryOptions{TDbContext}" /> to configure</param>
    public static void UsePostgreSqlOutboxProvider<TDbContext>(this OutboxDeliveryOptions<TDbContext> options)
        where TDbContext : DbContext
    {
        options.OutboxProvider = new PostgreSqlOutboxProvider<TDbContext>();
    }
}