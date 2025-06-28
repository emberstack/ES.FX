using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.MySql;

/// <summary>
///     Extension methods for configuring MySQL-specific outbox functionality.
/// </summary>
[PublicAPI]
public static class MySqlOutboxExtensions
{
    /// <summary>
    ///     Registers MySQL optimized services for the outbox delivery
    /// </summary>
    /// <typeparam name="TDbContext"><see cref="TDbContext" /> context for the options </typeparam>
    /// <param name="options">The <see cref="OutboxDeliveryOptions{TDbContext}" /> to configure</param>
    public static void UseMySqlOutboxProvider<TDbContext>(this OutboxDeliveryOptions<TDbContext> options)
        where TDbContext : DbContext
    {
        options.OutboxProvider = new MySqlOutboxProvider<TDbContext>();
    }
}