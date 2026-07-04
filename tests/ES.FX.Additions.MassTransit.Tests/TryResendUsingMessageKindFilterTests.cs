using System.Collections.Concurrent;
using System.Net.Mime;
using System.Reflection;
using ES.FX.Additions.MassTransit.MessageKind;
using ES.FX.ComponentModel.DataAnnotations;
using MassTransit;
using MassTransit.Serialization;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ES.FX.Additions.MassTransit.Tests;

/// <summary>
///     Functional coverage of <see cref="TryResendUsingMessageKindFilter" /> — the dead-letter resend filter and the
///     most complex logic in the library.
///     <para>
///         Strategy: the two passthrough branches where no serialization happens are covered with pure Moq (a strict
///         <see cref="ReceiveContext" /> proves the serializer is never touched). The successful resend and the
///         serializer-selection branches (default vs content-type cache) are driven by capturing a <b>real</b>
///         MassTransit JSON envelope from the in-memory harness and replaying it through a fully-mocked
///         <see cref="ReceiveContext" />: real body + content type make the concrete
///         <see cref="SystemTextJsonMessageSerializer" /> deserialize for real, while the mock lets us supply a live
///         <see cref="CancellationToken" /> and a spy <see cref="ISendEndpointProvider" /> so the resend can be
///         observed without a broker round-trip.
///     </para>
/// </summary>
public sealed class TryResendUsingMessageKindFilterTests
{
    private static (string Body, ContentType ContentType)? _capturedEnvelope;
    private static readonly SemaphoreSlim CaptureGate = new(1, 1);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Cast to the explicitly-implemented IFilter<ReceiveContext>.Send.
    private static Task InvokeSend(TryResendUsingMessageKindFilter filter, ReceiveContext context,
        IPipe<ReceiveContext> next) =>
        ((IFilter<ReceiveContext>)filter).Send(context, next);

    // =========================================================================================================
    // Branch: the kind header does not resolve to a type -> next.Send passthrough; serializer never touched.
    // Pure Moq (MockBehavior.Strict guarantees Body/ContentType are never read on this path).
    // =========================================================================================================

    [Fact]
    public async Task Send_HeaderMissing_CallsNextAndDoesNotResend()
    {
        var headers = new Mock<Headers>();
        object? none = null;
        headers.Setup(h => h.TryGetHeader(MessageKindProvider.Header, out none)).Returns(false);

        var context = new Mock<ReceiveContext>(MockBehavior.Strict);
        context.SetupGet(c => c.TransportHeaders).Returns(headers.Object);

        var next = new Mock<IPipe<ReceiveContext>>();
        next.Setup(n => n.Send(It.IsAny<ReceiveContext>())).Returns(Task.CompletedTask);

        await InvokeSend(new TryResendUsingMessageKindFilter(), context.Object, next.Object);

        next.Verify(n => n.Send(context.Object), Times.Once);
    }

    [Fact]
    public async Task Send_HeaderPresentButUnregisteredKind_CallsNextAndDoesNotResend()
    {
        var headers = new Mock<Headers>();
        object? value = "resend-never-registered-" + Guid.NewGuid();
        headers.Setup(h => h.TryGetHeader(MessageKindProvider.Header, out value)).Returns(true);

        var context = new Mock<ReceiveContext>(MockBehavior.Strict);
        context.SetupGet(c => c.TransportHeaders).Returns(headers.Object);

        var next = new Mock<IPipe<ReceiveContext>>();
        next.Setup(n => n.Send(It.IsAny<ReceiveContext>())).Returns(Task.CompletedTask);

        await InvokeSend(new TryResendUsingMessageKindFilter(), context.Object, next.Object);

        next.Verify(n => n.Send(context.Object), Times.Once);
    }

    // Captures a genuine MassTransit envelope (JSON string + content type) once. The provider is disposed
    // immediately afterwards: we only keep the serialized bytes, so nothing depends on live bus plumbing.
    private static async Task<(string Body, ContentType ContentType)> GetEnvelopeAsync()
    {
        await CaptureGate.WaitAsync(Ct);
        try
        {
            if (_capturedEnvelope is { } cached) return cached;

            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(cfg =>
                {
                    cfg.AddConsumer<CapturingConsumer>();
                    cfg.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();
            await harness.Bus.Publish(new ResendTarget(Guid.NewGuid()), Ct);
            Assert.True(await harness.Consumed.Any<ResendTarget>(Ct));

            _capturedEnvelope = await CapturingConsumer.Captured.Task.WaitAsync(Ct);
            return _capturedEnvelope.Value;
        }
        finally
        {
            CaptureGate.Release();
        }
    }

    // Builds a fully-mocked ReceiveContext around a raw envelope body. Body + content type are authentic (so the
    // real serializer deserializes), while TransportHeaders/InputAddress/CancellationToken/SendEndpointProvider
    // are controlled so the resend is observable.
    private static (ReceiveContext Context, Mock<ISendEndpoint> Endpoint, Uri InputAddress) BuildReplayContext(
        string body, ContentType? contentType, string kind, ISendEndpointProvider sendEndpointProvider)
    {
        var headers = new Mock<Headers>();
        object? headerValue = kind;
        headers.Setup(h => h.TryGetHeader(MessageKindProvider.Header, out headerValue)).Returns(true);

        var inputAddress = new Uri("loopback://localhost/resend-input");

        var context = new Mock<ReceiveContext>();
        context.SetupGet(c => c.TransportHeaders).Returns(headers.Object);
        context.SetupGet(c => c.ContentType).Returns(() => contentType!);
        context.SetupGet(c => c.Body).Returns(new StringMessageBody(body));
        context.SetupGet(c => c.InputAddress).Returns(inputAddress);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        context.SetupGet(c => c.SendEndpointProvider).Returns(sendEndpointProvider);

        return (context.Object, new Mock<ISendEndpoint>(), inputAddress);
    }

    private static (ISendEndpointProvider Provider, Mock<ISendEndpoint> Endpoint,
        Func<(object? Message, SendContext? Send)> Result) SpySendEndpoint()
    {
        object? sentMessage = null;
        SendContext? sentContext = null;

        var endpoint = new Mock<ISendEndpoint>();
        // MassTransit resolves ConsumeContext.Send(uri, object, IPipe<SendContext>) to the generic
        // ISendEndpoint.Send<T>(T, IPipe<SendContext<T>>, ct) where T is the resolved runtime type (ResendTarget)
        // and the pipe is a SendContext<T> adapter. Capture that specific overload and run the pipe against a
        // mock SendContext<ResendTarget> so the filter's RequestId/ResponseAddress/FaultAddress writes are visible.
        endpoint
            .Setup(e => e.Send(It.IsAny<ResendTarget>(), It.IsAny<IPipe<SendContext<ResendTarget>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ResendTarget message, IPipe<SendContext<ResendTarget>> pipe, CancellationToken _) =>
            {
                sentMessage = message;
                var sendContext = new Mock<SendContext<ResendTarget>>();
                sendContext.SetupAllProperties();
                await pipe.Send(sendContext.Object);
                sentContext = sendContext.Object;
            });

        var provider = new Mock<ISendEndpointProvider>();
        provider.Setup(p => p.GetSendEndpoint(It.IsAny<Uri>())).ReturnsAsync(endpoint.Object);

        return (provider.Object, endpoint, () => (sentMessage, sentContext));
    }

    // =========================================================================================================
    // Branch: kind resolves + message deserializes -> resend to InputAddress with request/response/fault copied.
    // =========================================================================================================

    [Fact]
    public async Task Send_KindResolves_ResendsToInputAddress_PropagatingRequestResponseFault()
    {
        var (rawBody, contentType) = await GetEnvelopeAsync();
        MessageKindProvider.RegisterTypes(typeof(ResendTarget));

        // Inject non-null request/response/fault fields into the real envelope so propagation is observable
        // (a plain publish leaves them null, which would make the copy assertions vacuous).
        var requestId = Guid.NewGuid();
        var responseAddress = "loopback://localhost/response-queue";
        var faultAddress = "loopback://localhost/fault-queue";
        var body = rawBody
            .Replace("\"requestId\": null", $"\"requestId\": \"{requestId}\"")
            .Replace("\"responseAddress\": null", $"\"responseAddress\": \"{responseAddress}\"")
            .Replace("\"faultAddress\": null", $"\"faultAddress\": \"{faultAddress}\"");

        var spy = SpySendEndpoint();
        var (context, _, inputAddress) =
            BuildReplayContext(body, contentType, "resend-target-8f21", spy.Provider);

        var next = new Mock<IPipe<ReceiveContext>>();
        next.Setup(n => n.Send(It.IsAny<ReceiveContext>())).Returns(Task.CompletedTask);

        await InvokeSend(new TryResendUsingMessageKindFilter(), context, next.Object);

        // Resent (not passed through) to the receive endpoint's own input address.
        next.Verify(n => n.Send(It.IsAny<ReceiveContext>()), Times.Never);
        Mock.Get(spy.Provider).Verify(p => p.GetSendEndpoint(inputAddress), Times.Once);

        var (sentMessage, sentContext) = spy.Result();
        var resent = Assert.IsType<ResendTarget>(sentMessage);
        Assert.NotEqual(Guid.Empty, resent.Id);

        Assert.NotNull(sentContext);
        Assert.Equal(requestId, sentContext!.RequestId);
        Assert.Equal(new Uri(responseAddress), sentContext.ResponseAddress);
        Assert.Equal(new Uri(faultAddress), sentContext.FaultAddress);
    }

    // =========================================================================================================
    // Branch: null ContentType -> the DefaultSerializer path is used (MassTransit's default JSON content type).
    // =========================================================================================================

    [Fact]
    public async Task Send_NullContentType_UsesDefaultSerializer_AndStillResends()
    {
        var (body, _) = await GetEnvelopeAsync();
        MessageKindProvider.RegisterTypes(typeof(ResendTarget));

        var spy = SpySendEndpoint();
        // contentType: null -> filter selects DefaultSerializer; the captured body uses the default MT JSON type,
        // so deserialization still succeeds and the message is resent.
        var (context, _, inputAddress) = BuildReplayContext(body, null, "resend-target-8f21", spy.Provider);

        var next = new Mock<IPipe<ReceiveContext>>();
        next.Setup(n => n.Send(It.IsAny<ReceiveContext>())).Returns(Task.CompletedTask);

        await InvokeSend(new TryResendUsingMessageKindFilter(), context, next.Object);

        next.Verify(n => n.Send(It.IsAny<ReceiveContext>()), Times.Never);
        Mock.Get(spy.Provider).Verify(p => p.GetSendEndpoint(inputAddress), Times.Once);
        Assert.IsType<ResendTarget>(spy.Result().Message);
    }

    // =========================================================================================================
    // Branch: non-null ContentType -> the content-type-keyed serializer cache (GetOrAdd) is populated and reused.
    // Confirmed by reflecting the private static cache and asserting a single entry keyed by media type persists
    // across two invocations with the same content type.
    // =========================================================================================================

    [Fact]
    public async Task Send_NonNullContentType_CachesSerializerByMediaType_AndReusesAcrossCalls()
    {
        var (body, contentType) = await GetEnvelopeAsync();
        MessageKindProvider.RegisterTypes(typeof(ResendTarget));

        var cache = GetSerializerCache();
        cache.TryRemove(contentType.MediaType, out _); // isolate from any prior test that primed the cache

        async Task ResendOnce()
        {
            var spy = SpySendEndpoint();
            var (context, _, _) = BuildReplayContext(body, contentType, "resend-target-8f21", spy.Provider);
            var next = new Mock<IPipe<ReceiveContext>>();
            next.Setup(n => n.Send(It.IsAny<ReceiveContext>())).Returns(Task.CompletedTask);
            await InvokeSend(new TryResendUsingMessageKindFilter(), context, next.Object);
            Assert.IsType<ResendTarget>(spy.Result().Message);
        }

        await ResendOnce();
        Assert.True(cache.TryGetValue(contentType.MediaType, out var firstSerializer));
        Assert.NotNull(firstSerializer);

        await ResendOnce();
        Assert.True(cache.TryGetValue(contentType.MediaType, out var secondSerializer));

        // GetOrAdd must return the SAME cached serializer instance for the same media type (no re-creation).
        Assert.Same(firstSerializer, secondSerializer);
    }

    // =========================================================================================================
    // Branch: "TryGetMessage fails -> next.Send". DOCUMENTED AS EFFECTIVELY UNREACHABLE with the real serializer.
    // The concrete SystemTextJsonMessageSerializer's SerializerContext.TryGetMessage(Type) never returns false for
    // a resolvable kind: for a shape-compatible concrete/record type it deserializes (returns true); for an
    // interface it builds a dynamic proxy (returns true); for an abstract type or a throwing constructor it
    // THROWS rather than returning false. There is thus no input that makes the branch return false without the
    // library injecting a custom serializer (it does not). This test asserts that CURRENT real behavior: pointing
    // the kind header at an unrelated interface still resends (proxy deserialization succeeds) instead of falling
    // through to next.Send. If a future serializer change makes TryGetMessage return false, this test — and the
    // filter's passthrough branch — should be revisited.
    [Fact]
    public async Task Send_KindResolvesToUnrelatedInterface_StjBuildsProxy_SoStillResends_NotPassthrough()
    {
        var (body, contentType) = await GetEnvelopeAsync();
        MessageKindProvider.RegisterTypes(typeof(IUnrelatedContract));
        var interfaceKind = KindAttribute.For(typeof(IUnrelatedContract))!;

        var spy = SpySendEndpoint();
        var (context, _, inputAddress) = BuildReplayContext(body, contentType, interfaceKind, spy.Provider);

        var next = new Mock<IPipe<ReceiveContext>>();
        next.Setup(n => n.Send(It.IsAny<ReceiveContext>())).Returns(Task.CompletedTask);

        await InvokeSend(new TryResendUsingMessageKindFilter(), context, next.Object);

        // Current real behavior: TryGetMessage(interface) succeeds via a dynamic proxy, so the message is resent
        // and next.Send is NOT invoked (the "fails -> passthrough" branch is not taken).
        Assert.Same(typeof(IUnrelatedContract), MessageKindProvider.GetType(interfaceKind));
        next.Verify(n => n.Send(It.IsAny<ReceiveContext>()), Times.Never);
        Mock.Get(spy.Provider).Verify(p => p.GetSendEndpoint(inputAddress), Times.Once);
    }

    private static ConcurrentDictionary<string, SystemTextJsonMessageSerializer> GetSerializerCache()
    {
        var field = typeof(TryResendUsingMessageKindFilter)
            .GetField("Serializers", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return (ConcurrentDictionary<string, SystemTextJsonMessageSerializer>)field!.GetValue(null)!;
    }

    // Contract used by the envelope-replay tests. A unique kind keeps the process-global provider cache clean.
    [Kind("resend-target-8f21")]
    public sealed record ResendTarget(Guid Id);

    // =========================================================================================================
    // Envelope-replay infrastructure: capture one real MassTransit JSON envelope for a ResendTarget message.
    // =========================================================================================================

    private sealed class CapturingConsumer : IConsumer<ResendTarget>
    {
        public static readonly TaskCompletionSource<(string Body, ContentType ContentType)> Captured =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Consume(ConsumeContext<ResendTarget> context)
        {
            Captured.TrySetResult((context.ReceiveContext.Body.GetString(), context.ReceiveContext.ContentType!));
            return Task.CompletedTask;
        }
    }

    [Kind("resend-unrelated-interface-contract")]
    public interface IUnrelatedContract
    {
        string SomeValue { get; }
    }
}