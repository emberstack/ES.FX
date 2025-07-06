using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.MessageBus.Infrastructure;

public interface IMessageBusEngine
{
    public IServiceCollection RegisterServices(IServiceCollection serviceCollection);
}