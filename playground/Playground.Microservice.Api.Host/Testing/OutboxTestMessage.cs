using ES.FX.TransactionalOutbox.Abstractions.Messages;
using MassTransit;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

[OutboxMessageType("OutboxTextMessage.v1")]
[EntityName("OutboxTextMessage.v1")]
public record OutboxTestMessage(string SomeProp) : INotification;