using ES.FX.Additions.MassTransit.MessageKind;
using ES.FX.ComponentModel.DataAnnotations;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ES.FX.Additions.MassTransit.Tests;

/// <summary>
///     End-to-end functional coverage of <see cref="MessageKindExtensions.UseMessageKind" /> using the MassTransit
///     in-memory test harness (no broker, no Docker). Verifies that:
///     <list type="bullet">
///         <item>the publish filter stamps the <see cref="MessageKindProvider.Header" /> on outgoing messages,</item>
///         <item>the stamped header resolves back to the .NET type via <see cref="MessageKindProvider" />,</item>
///         <item>the consumer-configuration observer registers consumed message types with the provider.</item>
///     </list>
///     Kind values used here are unique per test class so the process-global provider cache cannot collide with
///     other tests.
/// </summary>
public sealed class UseMessageKindHarnessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceProvider BuildHarnessProvider() =>
        new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<WidgetCreatedConsumer>();
                cfg.UsingInMemory((context, bus) =>
                {
                    bus.UseMessageKind(context);
                    bus.ConfigureEndpoints(context);
                });
            })
            .BuildServiceProvider(true);

    [Fact]
    public async Task Publish_StampsKindHeader_OnOutgoingMessage()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new WidgetCreated(Guid.NewGuid()), Ct);

        Assert.True(await harness.Published.Any<WidgetCreated>(Ct));

        var published = harness.Published.Select<WidgetCreated>(Ct).First();
        Assert.NotNull(published);
        Assert.True(published!.Context!.Headers.TryGetHeader(MessageKindProvider.Header, out var header));
        Assert.Equal("harness-widget-created", header?.ToString());
    }

    [Fact]
    public async Task Consumer_IsRegisteredWithProvider_ViaConfigurationObserver()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();

        // Starting the harness configures the consumer, which fires the observer that registers the message type.
        await harness.Start();

        Assert.Same(typeof(WidgetCreated), MessageKindProvider.GetType("harness-widget-created"));
    }

    [Fact]
    public async Task Publish_MessageIsConsumed_EndToEnd()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new WidgetCreated(Guid.NewGuid()), Ct);

        Assert.True(await harness.Consumed.Any<WidgetCreated>(Ct));

        var consumerHarness = harness.GetConsumerHarness<WidgetCreatedConsumer>();
        Assert.True(await consumerHarness.Consumed.Any<WidgetCreated>(Ct));
    }

    [Fact]
    public async Task Publish_UnkindedMessage_DoesNotStampHeader()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.UsingInMemory((context, bus) =>
                {
                    bus.UseMessageKind(context);
                    bus.ConfigureEndpoints(context);
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new PlainMessage(Guid.NewGuid()), Ct);

        Assert.True(await harness.Published.Any<PlainMessage>(Ct));
        var published = harness.Published.Select<PlainMessage>(Ct).First();
        Assert.NotNull(published);
        Assert.False(published!.Context!.Headers.TryGetHeader(MessageKindProvider.Header, out _));
    }

    [Fact]
    public void UseMessageKind_NullConfigurator_Throws()
    {
        // cfg is validated first via ArgumentNullException.ThrowIfNull(cfg), so this fails fast
        // regardless of the registration context argument.
        Assert.Throws<ArgumentNullException>(() =>
            MessageKindExtensions.UseMessageKind(null!, null!));
    }

    [Fact]
    public void UseMessageKind_NullRegistrationContext_Throws()
    {
        // With a valid configurator supplied, the null registration context must still be rejected.
        var cfg = new Mock<IBusFactoryConfigurator>().Object;

        Assert.Throws<ArgumentNullException>(() =>
            cfg.UseMessageKind(null!));
    }

    // Contract local to this test so its kind is unique and only registered through the harness.
    [Kind("harness-widget-created")]
    public sealed record WidgetCreated(Guid Id);

    public sealed class WidgetCreatedConsumer : IConsumer<WidgetCreated>
    {
        public Task Consume(ConsumeContext<WidgetCreated> context) => Task.CompletedTask;
    }
}