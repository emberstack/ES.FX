using JetBrains.Annotations;

namespace ES.FX.MessageBus.Abstractions;

[PublicAPI]
public interface IMessageBusControl : IAsyncDisposable
{
    /// <summary>
    /// Start receiving and dispatching messages.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop receiving new messages and finish in-flight work.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);


    /// <summary>
    /// Checks if the bus is ready
    /// </summary>
    ValueTask<bool> IsReadyAsync(CancellationToken cancellation = default);
}