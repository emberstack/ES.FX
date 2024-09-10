using ES.FX.Contracts.Messaging;
using ES.FX.Contracts.TransactionalOutbox;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

[MessageType("OutboxTextMessage.v1")]
public record OutboxTestMessage(string SomeProp) : IOutboxMessage, INotification;