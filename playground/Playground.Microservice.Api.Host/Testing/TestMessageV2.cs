using ES.FX.ComponentModel.DataAnnotations;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

[Kind("TestMessage.v2")]
public class TestMessageV2 : IRequest
{
    public required Guid Id { get; set; }
}