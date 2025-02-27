﻿using ES.FX.ComponentModel.DataAnnotations;
using ES.FX.TransactionalOutbox;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

[PayloadType("OutboxTextMessage.v1")]
public record OutboxTestMessage(string SomeProp) : IOutboxMessage, INotification;