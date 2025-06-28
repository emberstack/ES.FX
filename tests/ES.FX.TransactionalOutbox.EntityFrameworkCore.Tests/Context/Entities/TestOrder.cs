using JetBrains.Annotations;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context.Entities;

[PublicAPI]
public class TestOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}