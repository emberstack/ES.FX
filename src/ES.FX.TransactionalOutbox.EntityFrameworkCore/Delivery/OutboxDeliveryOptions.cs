﻿using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

/// <summary>
///     Base class for the options used by the <see cref="OutboxDeliveryService{TDbContext}" />
/// </summary>
public abstract class OutboxDeliveryOptions
{
    /// <summary>
    ///     The interval between polling for new messages. This will be interrupted by the signalling mechanism of new messages
    ///     getting added to the outbox.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     The timeout for the delivery of a batch. This includes acquiring the outbox, processing the messages and releasing
    ///     the lock.
    /// </summary>
    public TimeSpan? DeliveryTimeout { get; set; }


    /// <summary>
    ///     The timeout used to commit changes after processing a batch of messages.
    /// </summary>
    public TimeSpan? TransactionCommitTimeout { get; set; }


    /// <summary>
    ///     The isolation level for the transaction used to acquire the outbox and process the messages
    /// </summary>
    public IsolationLevel TransactionIsolationLevel { get; set; } = IsolationLevel.RepeatableRead;

    /// <summary>
    ///     The number of messages to retrieve in each batch
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    ///     Enable or disable the delivery service
    /// </summary>
    public bool DeliveryServiceEnabled { get; set; } = true;
}

/// <summary>
///     Options used by the <see cref="OutboxDeliveryService{TDbContext}" />
/// </summary>
/// <typeparam name="TDbContext">The <see cref="OutboxDeliveryService{TDbContext}" /></typeparam>
public class OutboxDeliveryOptions<TDbContext> : OutboxDeliveryOptions where TDbContext : DbContext
{
    /// <summary>
    ///     The provider that will be used to acquire the outbox. This should be provided by the specific database provider.
    /// </summary>
    public IOutboxProvider<TDbContext> OutboxProvider { get; set; } = new DefaultOutboxProvider<TDbContext>();
}