using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ES.FX.MessageBus.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.MessageBus
{
    public static class MessageBusExtensions
    {
        public static void AddMessageBus(this IServiceCollection services, Action<IMessageBusBuilder>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var builder = new MessageBusBuilder();
            configure?.Invoke(builder);


            if (builder.Engine is null)
            {
                throw new InvalidOperationException("MessageBus engine is not configured. Use UseEngine method to set the engine.");
            }
           
            builder.Engine.RegisterServices(services);
        }
    }
}
