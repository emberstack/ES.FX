namespace ES.FX.TransactionalOutbox.EntityFrameworkCore;

/// <summary>
///     Interface to decorate the DbContext for outbox extension methods. This is useful so the extension methods do not
///     pollute all DbContext types
/// </summary>
public interface IOutboxDbContext
{
}