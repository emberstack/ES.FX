namespace ES.FX.MessageBus;

public interface IMessageContext
{
    object Message { get; }
    public Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class;
}

public interface IMessageContext<out TMessage> : IMessageContext where TMessage : class
{
    new TMessage Message { get; }
}