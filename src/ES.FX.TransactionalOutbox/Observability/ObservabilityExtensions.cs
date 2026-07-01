using JetBrains.Annotations;
using OpenTelemetry.Trace;

namespace ES.FX.TransactionalOutbox.Observability;

/// <summary>
///     Extensions for adding outbox observability instrumentation.
/// </summary>
[PublicAPI]
public static class ObservabilityExtensions
{
    /// <summary>
    ///     Adds the outbox <see cref="Diagnostics.ActivitySource" /> to the <see cref="TracerProviderBuilder" />.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder" /> to add the source to</param>
    /// <returns>The <see cref="TracerProviderBuilder" /> for chaining</returns>
    public static TracerProviderBuilder AddOutboxInstrumentation(
        this TracerProviderBuilder builder) =>
        builder.AddSource(Diagnostics.ActivitySourceName);
}