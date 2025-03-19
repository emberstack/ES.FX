using ES.FX.ComponentModel.DataAnnotations;
using ES.FX.TransactionalOutbox;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

[PayloadType("OutboxTextMessage.v1")]
public class OutboxTestMessage : IOutboxMessage, IRequest
{
    public required string SomeProp { get; set; }
}