namespace ES.FX.TransactionalOutbox.Interceptors;

/// <summary>
///     Interface for intercepting outbox messages before they are added to the outbox.
///     Allows for custom logic to be applied to the outbox message context, such as modifying headers or inspecting the
///     payload.
/// </summary>
public interface IOutboxMessageInterceptor
{
    public void Intercept(OutboxMessageInterceptorContext context);
}