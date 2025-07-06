namespace ES.FX.MessageBus;

public interface IMessageBus
{
    Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class;
}