using ES.FX.Contracts.Payloads;
using ES.FX.Contracts.TransactionalOutbox;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

[PayloadType("OutboxTextMessage.v1")]
public record OutboxTestMessage(string SomeProp) : IOutboxMessage, INotification;