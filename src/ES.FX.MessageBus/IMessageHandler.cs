namespace ES.FX.MessageBus;


public interface IMessageHandler;

public interface IMessageHandler<in TMessage>: IMessageHandler where TMessage : class
{
    public Task Handle(IMessageContext<TMessage> context, CancellationToken cancellationToken = default);
}