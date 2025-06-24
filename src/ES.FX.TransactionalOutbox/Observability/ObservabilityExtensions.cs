using JetBrains.Annotations;
using OpenTelemetry.Trace;

namespace ES.FX.TransactionalOutbox.Observability;

[PublicAPI]
public static class ObservabilityExtensions
{
    public static TracerProviderBuilder AddOutboxInstrumentation(
        this TracerProviderBuilder builder) =>
        builder.AddSource(Diagnostics.ActivitySourceName);
}