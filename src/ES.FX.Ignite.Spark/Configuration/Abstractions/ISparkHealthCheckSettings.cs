namespace ES.FX.Ignite.Spark.Configuration.Abstractions;

public interface ISparkHealthCheckSettings
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the health checks are enabled.
    /// </summary>
    public bool HealthChecksEnabled { get; set; }
}