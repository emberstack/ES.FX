using System.Diagnostics;
using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Delivery.Actions;
using ES.FX.TransactionalOutbox.Delivery.Faults;
using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Internals;
using ES.FX.TransactionalOutbox.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

/// <summary>
///     Hosted service that delivers outbox messages
/// </summary>
/// <typeparam name="TDbContext"> The <see cref="TDbContext" /> from which to process messages </typeparam>
/// <param name="logger"> The <see cref="ILogger{TCategoryName}" /> </param>
/// <param name="serviceProvider"> The <see cref="IServiceProvider" /> </param>
public class OutboxDeliveryService<TDbContext>(
    ILogger<OutboxDeliveryService<TDbContext>> logger,
    IServiceProvider serviceProvider)
    : BackgroundService
    where TDbContext : DbContext
{
    private async Task DeliverMessage(
        OutboxMessage message,
        OutboxMessageContext messageContext,
        IOutboxMessageHandler messageHandler,
        CancellationToken cancellationToken)
    {
        Activity? deliverMessageActivity = null;
        try
        {
            logger.LogTrace("Delivering outbox/message:{outboxId}/{messageId} - Attempt {attempt}",
                message.OutboxId, message.Id, message.DeliveryAttempts);

            deliverMessageActivity = Diagnostics.ActivitySource.StartActivity(
                Diagnostics.DeliverMessageActivityName,
                ActivityKind.Server,
                message.ActivityId,
                new Dictionary<string, object?>
                {
                    { "outbox.id", message.OutboxId },
                    { "outbox.message.id", message.Id },
                    { "outbox.message.addedAt", message.AddedAt },
                    { "outbox.message.attempt", message.DeliveryAttempts },
                    { "outbox.dbContext.type", typeof(TDbContext).FullName }
                }, Activity.Current is null ? null : new[] { new ActivityLink(Activity.Current.Context) });


            await messageHandler
                .HandleAsync(messageContext, cancellationToken)
                .ConfigureAwait(false);
            deliverMessageActivity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception)
        {
            deliverMessageActivity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            deliverMessageActivity?.Stop();
            deliverMessageActivity?.Dispose();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogTrace("Starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            //Create a new scope for each iteration to avoid memory leaks
            await using var scope = serviceProvider.CreateAsyncScope();
            var outboxDeliveryOptions = scope.ServiceProvider
                .GetRequiredService<IOptionsMonitor<OutboxDeliveryOptions<TDbContext>>>()
                .CurrentValue;


            // Check if the delivery service is enabled. Sleep to refresh if not enabled.
            if (!outboxDeliveryOptions.DeliveryServiceEnabled)
            {
                await Sleep(outboxDeliveryOptions.PollingInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            //Check if the outbox provider is ready to deliver messages. If not, sleep and try again.
            var messageHandler =
                scope.ServiceProvider.GetRequiredKeyedService<IOutboxMessageHandler>(typeof(TDbContext));
            bool messageHandlerReady;
            try
            {
                using var handlerReadyTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken,
                    new CancellationTokenSource(outboxDeliveryOptions.DeliveryTimeout).Token);
                messageHandlerReady =
                    await messageHandler.IsReadyAsync(handlerReadyTimeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                messageHandlerReady = false;
            }

            if (!messageHandlerReady)
            {
                logger.LogTrace("The message handler is not ready yet.");
                await Sleep(outboxDeliveryOptions.PollingInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var messageFaultHandler =
                scope.ServiceProvider.GetRequiredKeyedService<IOutboxMessageFaultHandler>(typeof(TDbContext));

            //Always sleep unless there are more outboxes to process
            var sleep = true;


            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            var outboxDbContextOptions = dbContext.GetService<OutboxDbContextOptions>();

            try
            {
                //Use an execution strategy to handle transient exceptions. This is required for SQL Server with retries enabled
                await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                    {
                        using var deliveryTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken,
                            new CancellationTokenSource(outboxDeliveryOptions.DeliveryTimeout).Token);


                        await using var transaction = await dbContext.Database
                            .BeginTransactionAsync(outboxDeliveryOptions.TransactionIsolationLevel,
                                deliveryTimeout.Token)
                            .ConfigureAwait(false);


                        var outbox = await outboxDeliveryOptions.OutboxProvider
                            .GetNextExclusiveOutboxWithoutDelay(dbContext, deliveryTimeout.Token)
                            .ConfigureAwait(false);

                        if (outbox is null)
                        {
                            logger.LogTrace("No outbox available");
                            return;
                        }


                        try
                        {
                            //Set the lock for providers that do not support native locks. This updates the RowVersion
                            outbox.Lock = Guid.CreateVersion7();
                            outbox.DeliveryDelayedUntil = null;
                            dbContext.Update(outbox);
                            await dbContext.SaveChangesAsync(deliveryTimeout.Token).ConfigureAwait(false);
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            logger.LogTrace("Outbox {OutboxId} is locked by another consumer", outbox.Id);
                            sleep = false;
                            return;
                        }


                        Activity? deliverOutboxActivity = null;
                        try
                        {
                            logger.LogTrace("Processing outbox {OutboxId}", outbox.Id);
                            deliverOutboxActivity = Diagnostics.ActivitySource.StartActivity(
                                Diagnostics.DeliverOutboxActivityName,
                                ActivityKind.Client, null, new Dictionary<string, object?>
                                {
                                    { "outbox.dbContext.type", typeof(TDbContext).FullName }
                                }
                            );
                            deliverOutboxActivity?.Start();

                            var messages = await dbContext.Set<OutboxMessage>()
                                .Where(x => x.OutboxId == outbox.Id)
                                .OrderBy(x => x.Id)
                                .Take(outboxDeliveryOptions.BatchSize)
                                .AsTracking()
                                .ToListAsync(deliveryTimeout.Token)
                                .ConfigureAwait(false);


                            foreach (var message in messages)
                            {
                                if (DateTimeOffset.UtcNow > message.DeliveryNotAfter)
                                {
                                    logger.LogTrace("Outbox message {outboxId}/{messageId} has expired",
                                        message.OutboxId,
                                        message.Id);
                                    dbContext.Remove((object)message);
                                    continue;
                                }

                                if (DateTimeOffset.UtcNow < message.DeliveryNotBefore)
                                {
                                    logger.LogTrace(
                                        "Outbox message {outboxId}/{messageId} is not ready to be delivered until {notBefore}. Delaying outbox.",
                                        message.OutboxId,
                                        message.Id, message.DeliveryNotBefore);

                                    //Delay the entire outbox to preserve the order of messages
                                    outbox.DeliveryDelayedUntil = message.DeliveryNotBefore;

                                    //Proceed to the next available outbox, since this one is delayed
                                    break;
                                }

                                message.DeliveryFirstAttemptedAt ??= DateTimeOffset.UtcNow;
                                message.DeliveryLastAttemptedAt = DateTimeOffset.UtcNow;
                                message.DeliveryAttempts++;

                                outboxDbContextOptions.Serializer.Deserialize(
                                    message.Payload,
                                    message.PayloadType,
                                    message.Headers,
                                    out var payload,
                                    out var payloadType,
                                    out var headers);

                                var messageContext = new OutboxMessageContext
                                {
                                    Headers = headers,
                                    Message = payload,
                                    MessageType = payloadType,
                                    DeliveryAttempts = message.DeliveryAttempts,
                                    DeliveryFirstAttemptedAt =
                                        message.DeliveryFirstAttemptedAt ?? DateTimeOffset.UtcNow,
                                    DeliveryLastAttemptedAt = message.DeliveryLastAttemptedAt
                                };

                                try
                                {
                                    await DeliverMessage(message, messageContext, messageHandler, deliveryTimeout.Token)
                                        .ConfigureAwait(false);

                                    dbContext.Remove(message);
                                }
                                catch (Exception exception)
                                {
                                    logger.LogError(exception,
                                        "Outbox message {outboxId}/{messageId} delivery faulted due to exception.",
                                        message.OutboxId,
                                        message.Id);


                                    var faultResult = await messageFaultHandler.HandleAsync(
                                        new OutboxMessageFaultContext
                                        {
                                            MessageContext = messageContext,
                                            FaultException = exception
                                        }, deliveryTimeout.Token);


                                    if (faultResult.Action is RedeliverMessageAction redeliverMessageAction)
                                    {
                                        logger.LogDebug(
                                            "Outbox message {outboxId}/{messageId} will be redelivered as a result of fault handling. " +
                                            "Delaying delivery of outbox using delay: {delay}",
                                            message.OutboxId, message.Id, redeliverMessageAction.Delay);
                                        outbox.DeliveryDelayedUntil =
                                            DateTimeOffset.UtcNow.Add(redeliverMessageAction.Delay);

                                        break;
                                    }

                                    if (faultResult.Action is DiscardMessageAction)
                                    {
                                        logger.LogDebug(
                                            "Outbox message {outboxId}/{messageId} will be discarded as a result of fault handling.",
                                            message.OutboxId, message.Id);
                                        dbContext.Remove(message);
                                        continue;
                                    }

                                    throw new NotSupportedException(
                                        $"Action of type {faultResult.Action.GetType()} is not supported");
                                }
                            }


                            outbox.Lock = null;
                            dbContext.Update(outbox);

                            if (messages.Count == 0)
                            {
                                logger.LogTrace("Removing empty outbox {outboxId}", outbox.Id);
                                dbContext.Remove(outbox);
                            }

                            try
                            {
                                await dbContext.SaveChangesAsync(deliveryTimeout.Token).ConfigureAwait(false);
                                await transaction.CommitAsync(deliveryTimeout.Token).ConfigureAwait(false);
                            }
                            catch (Exception exception)
                            {
                                logger.LogError(exception, "Exception occurred while saving outbox");
                                try
                                {
                                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                                }
                                catch (Exception innerException)
                                {
                                    logger.LogError(innerException, "Transaction rollback failed");
                                }
                            }

                            sleep = false;
                            deliverOutboxActivity?.SetStatus(ActivityStatusCode.Ok);
                        }
                        catch (OperationCanceledException)
                        {
                            //Ignored
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(exception, "Exception occurred while processing an outbox");
                            deliverOutboxActivity?.SetStatus(ActivityStatusCode.Error);
                            throw;
                        }
                        finally
                        {
                            deliverOutboxActivity?.Stop();
                            deliverOutboxActivity?.Dispose();
                        }
                    }
                ).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The operation was cancelled, likely due to the stopping token being triggered
            }

            await dbContext.DisposeAsync();

            if (sleep) await Sleep(outboxDeliveryOptions.PollingInterval, stoppingToken).ConfigureAwait(false);
        }
    }


    private async Task Sleep(TimeSpan delay, CancellationToken cancellationToken)
    {
        logger.LogTrace("Sleeping for {delay}", delay);
        OutboxDeliverySignal.RenewChannel<TDbContext>();

        using var delayTokenCts = new CancellationTokenSource(delay);
        using var sleepCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, delayTokenCts.Token);
        string source;
        try
        {
            source = await OutboxDeliverySignal.GetChannel<TDbContext>().Reader.ReadAsync(sleepCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            source = nameof(OutboxDeliveryOptions<TDbContext>.PollingInterval);
        }

        logger.LogTrace("Sleep interrupted by {source}", source);
    }
}