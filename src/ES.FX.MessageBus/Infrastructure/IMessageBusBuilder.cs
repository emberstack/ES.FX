namespace ES.FX.MessageBus.Infrastructure;

public interface IMessageBusBuilder
{
    public IMessageBusEngine? Engine { get; set; }
}