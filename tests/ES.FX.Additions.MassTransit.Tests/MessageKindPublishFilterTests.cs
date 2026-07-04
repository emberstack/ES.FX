using ES.FX.Additions.MassTransit.MessageKind;
using ES.FX.ComponentModel.DataAnnotations;
using MassTransit;
using Moq;

namespace ES.FX.Additions.MassTransit.Tests;

/// <summary>
///     Direct functional coverage of <see cref="MessageKindPublishFilter{T}" />.
///     <see cref="MessageKindPublishFilter{T}.Send" />.
///     <para>
///         Focus is the kind-resolution logic and header stamping. <see cref="PublishContext{T}" /> and its
///         <see cref="SendHeaders" /> are interfaces, so they are mocked; no broker is required. Each test asserts both
///         that the pipeline continues (<c>next.Send</c> is invoked exactly once) and whether the kind header was set.
///     </para>
/// </summary>
public sealed class MessageKindPublishFilterTests
{
    private static (Mock<PublishContext<T>> Context, Mock<SendHeaders> Headers, Mock<IPipe<PublishContext<T>>> Next)
        SetupContext<T>(T message) where T : class
    {
        var headers = new Mock<SendHeaders>();
        var context = new Mock<PublishContext<T>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.Headers).Returns(headers.Object);

        var next = new Mock<IPipe<PublishContext<T>>>();
        next.Setup(n => n.Send(It.IsAny<PublishContext<T>>())).Returns(Task.CompletedTask);
        return (context, headers, next);
    }

    [Fact]
    public async Task Send_ConcreteKindedMessage_StampsKindFromRuntimeType()
    {
        var (context, headers, next) = SetupContext(new OrderCreated(Guid.NewGuid()));
        var filter = new MessageKindPublishFilter<OrderCreated>();

        await filter.Send(context.Object, next.Object);

        headers.Verify(h => h.Set(MessageKindProvider.Header, "order-created"), Times.Once);
        next.Verify(n => n.Send(context.Object), Times.Once);
    }

    [Fact]
    public async Task Send_InterfaceContract_RuntimeTypeUnkinded_FallsBackToGenericT()
    {
        // Idiomatic MassTransit: publish an interface contract. The concrete runtime instance (AccountOpened)
        // carries no [Kind]; the filter must fall back to KindAttribute.For<T>() where T = IAccountOpened.
        // This exercises the "KindAttribute.For(runtimeType) is null -> ?? KindAttribute.For<T>()" branch.
        var (context, headers, next) = SetupContext<IAccountOpened>(new AccountOpened(Guid.NewGuid()));

        // Guard: the concrete type genuinely has no kind, so only the T-fallback can produce the header.
        Assert.Null(KindAttribute.For(typeof(AccountOpened)));

        var filter = new MessageKindPublishFilter<IAccountOpened>();

        await filter.Send(context.Object, next.Object);

        headers.Verify(h => h.Set(MessageKindProvider.Header, "account-opened"), Times.Once);
        next.Verify(n => n.Send(context.Object), Times.Once);
    }

    [Fact]
    public async Task Send_UnkindedMessage_DoesNotStampHeaderButStillForwards()
    {
        var (context, headers, next) = SetupContext(new PlainMessage(Guid.NewGuid()));
        var filter = new MessageKindPublishFilter<PlainMessage>();

        await filter.Send(context.Object, next.Object);

        headers.Verify(h => h.Set(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        headers.Verify(h => h.Set(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()), Times.Never);
        next.Verify(n => n.Send(context.Object), Times.Once);
    }
}