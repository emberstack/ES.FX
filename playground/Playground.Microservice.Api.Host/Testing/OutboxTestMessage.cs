using ES.FX.ComponentModel.DataAnnotations;
using ES.FX.Messaging;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

[PayloadType("OutboxTextMessage.v1")]
public class OutboxTestMessage : IMessage, IRequest
{
    public required string SomeProp { get; set; }
}