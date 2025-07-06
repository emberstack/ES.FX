using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ES.FX.MessageBus.Infrastructure;

internal class MessageBusBuilder: IMessageBusBuilder
{
    public IMessageBusEngine? Engine { get; set; }
}