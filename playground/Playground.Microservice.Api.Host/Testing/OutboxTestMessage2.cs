using ES.FX.ComponentModel.DataAnnotations;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

[Kind("OutboxTextMessage2.v1")]
public class OutboxTestMessage2 : IRequest
{
    public required string SomeProp { get; set; }
}