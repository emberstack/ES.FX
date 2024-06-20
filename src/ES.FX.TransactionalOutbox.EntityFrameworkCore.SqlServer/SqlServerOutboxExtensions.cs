using ES.FX.Messaging;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer;

public static class SqlServerOutboxExtensions
{
    /// <summary>
    ///     Registers SQL Server optimized services for the outbox delivery
    /// </summary>
    /// <typeparam name="TDbContext"><see cref="TDbContext" /> context for the options </typeparam>
    /// <param name="options">The <see cref="OutboxDeliveryOptions{TDbContext}" /> to configure</param>
    public static void UseSqlServer<TDbContext>(this OutboxDeliveryOptions<TDbContext> options)
        where TDbContext : DbContext, IMessageStore
    {
        options.OutboxProvider = new SqlServerOutboxProvider<TDbContext>();
    }
}