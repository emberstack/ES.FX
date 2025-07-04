namespace ES.FX.MessageBus.Abstractions;

public interface IMessageHandler<in TMessage> where TMessage : class
{
    Task Handle(TMessage message, CancellationToken cancellationToken = default);
}