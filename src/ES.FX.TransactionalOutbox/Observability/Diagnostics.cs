using System.Diagnostics;
using ES.FX.TransactionalOutbox.Entities;

namespace ES.FX.TransactionalOutbox.Observability;

public static class Diagnostics
{
    public static readonly string ActivitySourceName = typeof(Outbox).Assembly.GetName().Name!;
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly string DeliverOutboxActivityName = $"{nameof(TransactionalOutbox)}.DeliverOutbox";
    public static readonly string DeliverMessageActivityName = $"{nameof(TransactionalOutbox)}.DeliverMessage";
}