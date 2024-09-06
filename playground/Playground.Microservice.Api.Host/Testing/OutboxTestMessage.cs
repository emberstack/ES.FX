using ES.FX.TransactionalOutbox.EntityFrameworkCore.Messages;

namespace Playground.Microservice.Api.Host.Testing;

[OutboxMessageType("SomeTestMessage")]
public record OutboxTestMessage(string SomeProp);