using JetBrains.Annotations;

namespace ES.FX.Messaging;

/// <summary>
/// Interface used to define an <see cref="IMessenger"/> that sends <see cref="IMessage"/>
/// </summary>
[PublicAPI]
public interface IMessenger
{
    /// <summary>
    /// Sends an <see cref="IMessage"/>
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used for the operation</param>
    public void Send(IMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an <see cref="IMessage"/>
    /// </summary>
    /// <param name="message">The message</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used for the operation</param>
    public Task SendAsync(IMessage message, CancellationToken cancellationToken = default);
}