using MassTransit;

namespace ES.FX.MessageBus.MassTransit.Internals;

internal class MassTransitMessageBusControl(IBusControl busControl) : IMessageBusControl
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken = default) =>
        await busControl.StartAsync(cancellationToken).ConfigureAwait(false);

    public async Task StopAsync(CancellationToken cancellationToken = default) =>
        await busControl.StopAsync(cancellationToken).ConfigureAwait(false);

    public async ValueTask<bool> IsReadyAsync(CancellationToken cancellation = default) =>
        await busControl.WaitForHealthStatus(BusHealthStatus.Healthy, cancellation).ConfigureAwait(false) ==
        BusHealthStatus.Healthy;
}