using ES.FX.Additions.MassTransit.Formatters;
using MassTransit;

namespace ES.FX.Additions.MassTransit.Tests;

/// <summary>
///     Functional coverage of the entity/endpoint name formatters. These are pure and require no broker.
/// </summary>
public sealed class FormatterTests
{
    // ---------- KindEntityNameFormatter ----------

    [Fact]
    public void KindEntityNameFormatter_KindedMessage_UsesKind()
    {
        var formatter = new KindEntityNameFormatter(new StubEntityNameFormatter());

        Assert.Equal("order-created", formatter.FormatEntityName<OrderCreated>());
    }

    [Fact]
    public void KindEntityNameFormatter_UnkindedMessage_FallsBackToBase()
    {
        var formatter = new KindEntityNameFormatter(new StubEntityNameFormatter());

        Assert.Equal("base:PlainMessage", formatter.FormatEntityName<PlainMessage>());
    }

    [Fact]
    public void KindEntityNameFormatter_FaultWithExplicitFaultKind_UsesFaultKind()
    {
        var formatter = new KindEntityNameFormatter(new StubEntityNameFormatter());

        // PaymentRequested has [FaultKind("payment-fault")] which wins over the Kind fallback.
        Assert.Equal("payment-fault", formatter.FormatEntityName<Fault<PaymentRequested>>());
    }

    [Fact]
    public void KindEntityNameFormatter_FaultWithoutFaultKind_FallsBackToKindFormat()
    {
        var formatter = new KindEntityNameFormatter(new StubEntityNameFormatter());

        // InvoiceIssued has a Kind but no FaultKind; default faultFormat "{0}_fault" is applied.
        Assert.Equal("invoice-issued_fault", formatter.FormatEntityName<Fault<InvoiceIssued>>());
    }

    [Fact]
    public void KindEntityNameFormatter_FaultWithoutFaultKind_CustomFaultFormat_IsHonored()
    {
        var formatter = new KindEntityNameFormatter(new StubEntityNameFormatter(), faultFormat: "fault-{0}");

        Assert.Equal("fault-invoice-issued", formatter.FormatEntityName<Fault<InvoiceIssued>>());
    }

    [Fact]
    public void KindEntityNameFormatter_FaultFallbackDisabled_UsesBaseForKindOnlyFault()
    {
        var formatter = new KindEntityNameFormatter(new StubEntityNameFormatter(), false);

        // With fallback disabled and no FaultKind, it defers to the base formatter for the Fault<> type.
        Assert.Equal("base:" + typeof(Fault<InvoiceIssued>).Name,
            formatter.FormatEntityName<Fault<InvoiceIssued>>());
    }

    [Fact]
    public void KindEntityNameFormatter_FaultFallbackDisabled_StillUsesExplicitFaultKind()
    {
        var formatter = new KindEntityNameFormatter(new StubEntityNameFormatter(), false);

        // Explicit FaultKind is honored regardless of the fallback flag.
        Assert.Equal("payment-fault", formatter.FormatEntityName<Fault<PaymentRequested>>());
    }

    // ---------- AggregatePrefixEntityNameFormatter ----------

    [Fact]
    public void AggregatePrefix_NoProviders_ReturnsBaseNameUnchanged()
    {
        var formatter = new AggregatePrefixEntityNameFormatter(new StubEntityNameFormatter());

        Assert.Equal("base:OrderCreated", formatter.FormatEntityName<OrderCreated>());
    }

    [Fact]
    public void AggregatePrefix_AppliesPrefixesInOrderWithSeparator()
    {
        var formatter = new AggregatePrefixEntityNameFormatter(
            new StubEntityNameFormatter(),
            "-",
            _ => "tenant",
            _ => "env");

        Assert.Equal("tenant-env-base:OrderCreated", formatter.FormatEntityName<OrderCreated>());
    }

    [Fact]
    public void AggregatePrefix_SkipsNullAndWhitespacePrefixes()
    {
        var formatter = new AggregatePrefixEntityNameFormatter(
            new StubEntityNameFormatter(),
            ".",
            _ => null,
            _ => "   ",
            _ => "keep");

        Assert.Equal("keep.base:OrderCreated", formatter.FormatEntityName<OrderCreated>());
    }

    [Fact]
    public void AggregatePrefix_NoSeparator_ConcatenatesDirectly()
    {
        var formatter = new AggregatePrefixEntityNameFormatter(
            new StubEntityNameFormatter(),
            prefixProviders: [_ => "a", _ => "b"]);

        Assert.Equal("abbase:OrderCreated", formatter.FormatEntityName<OrderCreated>());
    }

    [Fact]
    public void AggregatePrefix_PrefixProviderReceivesMessageType()
    {
        Type? seen = null;
        var formatter = new AggregatePrefixEntityNameFormatter(
            new StubEntityNameFormatter(),
            prefixProviders:
            [
                t =>
                {
                    seen = t;
                    return "x";
                }
            ]);

        formatter.FormatEntityName<OrderShipped>();

        Assert.Equal(typeof(OrderShipped), seen);
    }

    // ---------- KindEndpointNameFormatter ----------

    [Fact]
    public void KindEndpointNameFormatter_KindedType_UsesKind()
    {
        var formatter = new KindEndpointNameFormatter();

        Assert.Equal("order-created", formatter.Message<OrderCreated>());
    }

    [Fact]
    public void KindEndpointNameFormatter_KindedType_AppliesPrefix()
    {
        var formatter = new KindEndpointNameFormatter(prefix: "Dev");

        Assert.Equal("Devorder-created", formatter.Message<OrderCreated>());
    }

    [Fact]
    public void KindEndpointNameFormatter_UnkindedType_DelegatesToDefaultFormatter()
    {
        var kindFormatter = new KindEndpointNameFormatter();
        var defaultFormatter = DefaultEndpointNameFormatter.Instance;

        // For a type without [Kind], the base DefaultEndpointNameFormatter behavior is preserved.
        Assert.Equal(defaultFormatter.Message<PlainMessage>(), kindFormatter.Message<PlainMessage>());
    }

    /// <summary>Records what the base formatter was asked to format so fallthrough can be asserted.</summary>
    private sealed class StubEntityNameFormatter : IEntityNameFormatter
    {
        public string FormatEntityName<TMessage>() => "base:" + typeof(TMessage).Name;
    }
}