using ES.FX.ComponentModel.DataAnnotations;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

[Kind("TextMessage")]
public class TestMessage : IRequest
{
    public required Guid Id { get; set; }
}