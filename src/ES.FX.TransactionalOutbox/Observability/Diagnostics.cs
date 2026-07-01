using System.Diagnostics;
using ES.FX.TransactionalOutbox.Entities;

namespace ES.FX.TransactionalOutbox.Observability;

/// <summary>
///     Diagnostics definitions for outbox instrumentation.
/// </summary>
public static class Diagnostics
{
    /// <summary>
    ///     The name of the <see cref="System.Diagnostics.ActivitySource" /> used for outbox activities.
    /// </summary>
    public static readonly string ActivitySourceName = typeof(Outbox).Assembly.GetName().Name!;

    /// <summary>
    ///     The <see cref="System.Diagnostics.ActivitySource" /> used for outbox activities.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    ///     The name of the activity created when delivering an outbox.
    /// </summary>
    public static readonly string DeliverOutboxActivityName = $"{nameof(TransactionalOutbox)}.DeliverOutbox";

    /// <summary>
    ///     The name of the activity created when delivering an outbox message.
    /// </summary>
    public static readonly string DeliverMessageActivityName = $"{nameof(TransactionalOutbox)}.DeliverMessage";
}