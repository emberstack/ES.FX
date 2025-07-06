using JetBrains.Annotations;

namespace ES.FX.MessageBus;

[PublicAPI]
public class MessageHandlerDefinition<TMessage, TMessageHandler>
    where TMessage : class
    where TMessageHandler : class, IMessageHandler<TMessage>;

