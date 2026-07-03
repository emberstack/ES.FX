using System.Collections;
using ES.FX.Additions.MassTransit.MediatR.Consumers;
using ES.FX.Additions.MediatR.Contracts.Batches;
using MassTransit;
using MediatR;
using Moq;

namespace ES.FX.Additions.MassTransit.MediatR.Tests;

/// <summary>
///     Direct (harness-free) coverage of <see cref="MediatorBatchConsumer{TMessage}" />.Consume for the
///     branches the in-memory harness cannot deterministically drive:
///     <list type="bullet">
///         <item>the empty-batch early return (a real broker never forms a zero-length batch);</item>
///         <item>exact <see cref="CancellationToken" /> forwarding into <c>IMediator.Publish</c>/<c>Send</c>;</item>
///         <item>the unsupported element-type branch that throws <see cref="InvalidOperationException" />.</item>
///     </list>
///     A hand-rolled <see cref="Batch{T}" /> is fed through a mocked <see cref="ConsumeContext{T}" /> so the
///     batch length and the exact token are under the test's control.
/// </summary>
public class MediatorBatchConsumerDirectTests
{
    private static ConsumeContext<Batch<TMessage>> BuildContext<TMessage>(
        Batch<TMessage> batch, CancellationToken cancellationToken)
        where TMessage : class
    {
        var context = new Mock<ConsumeContext<Batch<TMessage>>>(MockBehavior.Loose);
        context.SetupGet(c => c.Message).Returns(batch);
        context.SetupGet(c => c.CancellationToken).Returns(cancellationToken);
        return context.Object;
    }

    [Fact]
    public async Task Batch_consumer_returns_without_dispatch_for_empty_batch()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var consumer = new MediatorBatchConsumer<NotificationMessage>(mediator.Object);
        var context = BuildContext(FakeBatch<NotificationMessage>.Empty, CancellationToken.None);

        // Must complete without throwing and without touching the mediator (Strict would throw on any call).
        await consumer.Consume(context);

        mediator.Verify(m => m.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(
            m => m.Send(It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Batch_consumer_forwards_context_cancellation_token_to_publish()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Loose);
        var consumer = new MediatorBatchConsumer<NotificationMessage>(mediator.Object);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var batch = FakeBatch<NotificationMessage>.Of(new NotificationMessage(Guid.NewGuid()));
        var context = BuildContext(batch, token);

        await consumer.Consume(context);

        // The exact context token — not CancellationToken.None/default — must be forwarded.
        mediator.Verify(m => m.Publish(
                It.IsAny<BatchNotification<NotificationMessage>>(),
                It.Is<CancellationToken>(t => t == token)),
            Times.Once);
    }

    [Fact]
    public async Task Batch_consumer_forwards_context_cancellation_token_to_send()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Loose);
        var consumer = new MediatorBatchConsumer<RequestMessage>(mediator.Object);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var batch = FakeBatch<RequestMessage>.Of(new RequestMessage(Guid.NewGuid()));
        var context = BuildContext(batch, token);

        await consumer.Consume(context);

        mediator.Verify(m => m.Send(
                It.Is<BatchRequest<RequestMessage>>(b => b.Items.Count == 1),
                It.Is<CancellationToken>(t => t == token)),
            Times.Once);
    }

    [Fact]
    public async Task Batch_consumer_throws_for_unsupported_element_type()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var consumer = new MediatorBatchConsumer<UnsupportedMessage>(mediator.Object);
        var batch = FakeBatch<UnsupportedMessage>.Of(new UnsupportedMessage(Guid.NewGuid()));
        var context = BuildContext(batch, CancellationToken.None);

        // UnsupportedMessage is neither INotification nor IRequest: the static typeof gate falls through
        // to the else branch and throws — distinct from the single consumer's runtime pattern-match path.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.Consume(context));
        Assert.Contains(nameof(UnsupportedMessage), ex.Message);

        // Nothing was dispatched (Strict mock would have thrown on any call).
        mediator.VerifyNoOtherCalls();
    }

    /// <summary>
    ///     Minimal in-memory <see cref="Batch{T}" /> (which is an
    ///     <see cref="IReadOnlyList{T}" /> of <see cref="ConsumeContext{T}" />) whose length and elements are
    ///     fully controlled by the test. Only the members the consumer touches (<c>Length</c> and the
    ///     indexer, each element's <c>.Message</c>) are meaningful; the rest satisfy the interface.
    /// </summary>
    private sealed class FakeBatch<T> : Batch<T> where T : class
    {
        private readonly ConsumeContext<T>[] _items;

        private FakeBatch(T[] messages)
        {
            _items = messages
                .Select(m => Mock.Of<ConsumeContext<T>>(c => c.Message == m))
                .ToArray();
        }

        public static FakeBatch<T> Empty { get; } = new([]);

        public static FakeBatch<T> Of(params T[] messages) => new(messages);

        public IEnumerator<ConsumeContext<T>> GetEnumerator() =>
            ((IEnumerable<ConsumeContext<T>>)_items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        public int Count => _items.Length;

        public ConsumeContext<T> this[int index] => _items[index];

        public BatchCompletionMode Mode => BatchCompletionMode.Size;

        public int Length => _items.Length;

        public DateTime FirstMessageReceived => DateTime.UtcNow;

        public DateTime LastMessageReceived => DateTime.UtcNow;
    }
}
