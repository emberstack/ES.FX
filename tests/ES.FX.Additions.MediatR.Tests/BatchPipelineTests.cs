using ES.FX.Additions.MediatR.Contracts.Batches;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Additions.MediatR.Tests;

/// <summary>
///     Functional tests that route the batch contract types through a real MediatR pipeline
///     wired up via DI. These verify the contracts actually work as MediatR messages end-to-end.
/// </summary>
public class BatchPipelineTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        // MediatR 14.x resolves ILoggerFactory during its startup license check; provide a no-op one.
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<CapturedItems>();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<BatchPipelineTests>());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task BatchRequest_IsDispatchedToRequestHandler()
    {
        var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var sink = provider.GetRequiredService<CapturedItems>();

        await mediator.Send(new BatchRequest<int> { Items = [10, 20, 30] },
            TestContext.Current.CancellationToken);

        Assert.Equal([10, 20, 30], sink.RequestItems);
    }

    [Fact]
    public async Task BatchRequest_EmptyBatch_HandlerStillInvokedWithEmptyItems()
    {
        var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var sink = provider.GetRequiredService<CapturedItems>();

        await mediator.Send(new BatchRequest<int>(), TestContext.Current.CancellationToken);

        Assert.NotNull(sink.RequestItems);
        Assert.Empty(sink.RequestItems!);
    }

    [Fact]
    public async Task BatchNotification_IsDispatchedToAllNotificationHandlers()
    {
        var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var sink = provider.GetRequiredService<CapturedItems>();

        await mediator.Publish(new BatchNotification<string> { Items = ["a", "b"] },
            TestContext.Current.CancellationToken);

        // Two independent handlers both observe the same notification.
        Assert.Equal(["a", "b"], sink.NotificationItemsHandlerOne);
        Assert.Equal(["a", "b"], sink.NotificationItemsHandlerTwo);
    }

    [Fact]
    public async Task BatchRequest_OfDifferentClosedGenerics_ResolveDistinctHandlers()
    {
        var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var sink = provider.GetRequiredService<CapturedItems>();

        await mediator.Send(new BatchRequest<int> { Items = [1] },
            TestContext.Current.CancellationToken);
        await mediator.Send(new BatchRequest<string> { Items = ["s"] },
            TestContext.Current.CancellationToken);

        Assert.Equal([1], sink.RequestItems);
        Assert.Equal(["s"], sink.StringRequestItems);
    }

    // ---- Shared capture sink (registered as singleton so handlers and test see the same instance) ----

    public sealed class CapturedItems
    {
        public IReadOnlyList<int>? RequestItems { get; set; }
        public IReadOnlyList<string>? StringRequestItems { get; set; }
        public IReadOnlyList<string>? NotificationItemsHandlerOne { get; set; }
        public IReadOnlyList<string>? NotificationItemsHandlerTwo { get; set; }
    }

    // ---- Handlers under test ----

    public sealed class IntBatchRequestHandler(CapturedItems captured)
        : IRequestHandler<BatchRequest<int>>
    {
        public Task Handle(BatchRequest<int> request, CancellationToken cancellationToken)
        {
            captured.RequestItems = request.Items;
            return Task.CompletedTask;
        }
    }

    public sealed class StringBatchRequestHandler(CapturedItems captured)
        : IRequestHandler<BatchRequest<string>>
    {
        public Task Handle(BatchRequest<string> request, CancellationToken cancellationToken)
        {
            captured.StringRequestItems = request.Items;
            return Task.CompletedTask;
        }
    }

    public sealed class BatchNotificationHandlerOne(CapturedItems captured)
        : INotificationHandler<BatchNotification<string>>
    {
        public Task Handle(BatchNotification<string> notification, CancellationToken cancellationToken)
        {
            captured.NotificationItemsHandlerOne = notification.Items;
            return Task.CompletedTask;
        }
    }

    public sealed class BatchNotificationHandlerTwo(CapturedItems captured)
        : INotificationHandler<BatchNotification<string>>
    {
        public Task Handle(BatchNotification<string> notification, CancellationToken cancellationToken)
        {
            captured.NotificationItemsHandlerTwo = notification.Items;
            return Task.CompletedTask;
        }
    }
}