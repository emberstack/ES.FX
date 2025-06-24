using ES.FX.ComponentModel.DataAnnotations;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

[PayloadType("OutboxTextMessage.v1")]
public class OutboxTestMessage : IRequest
{
    public required string SomeProp { get; set; }
}