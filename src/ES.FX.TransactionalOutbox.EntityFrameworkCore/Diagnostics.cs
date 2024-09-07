using System.Diagnostics;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Entities;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore;

public static class Diagnostics
{
    public static readonly string ActivitySourceName = typeof(Outbox).Assembly.GetName().Name!;
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly string DeliverMessageActivityName = $"{nameof(TransactionalOutbox)}.DeliverMessage";
}