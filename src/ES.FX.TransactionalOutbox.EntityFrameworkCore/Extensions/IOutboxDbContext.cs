using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Extensions;

/// <summary>
///     Interface used to decorate <see cref="DbContext" /> with outbox functionality.
/// </summary>
[PublicAPI]
public interface IOutboxDbContext;