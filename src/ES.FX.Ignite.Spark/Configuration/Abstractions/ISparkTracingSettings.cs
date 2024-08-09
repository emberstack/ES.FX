namespace ES.FX.Ignite.Spark.Configuration.Abstractions;

public interface ISparkTracingSettings
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is enabled.
    /// </summary>
    public bool TracingEnabled { get; set; }
}