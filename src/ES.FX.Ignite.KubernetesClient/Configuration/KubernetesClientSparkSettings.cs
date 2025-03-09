using ES.FX.Ignite.Spark.Configuration;
using k8s;

namespace ES.FX.Ignite.KubernetesClient.Configuration;

/// <summary>
///     Provides the settings for connecting to a Kubernetes cluster using a <see cref="IKubernetes" />
/// </summary>
public class KubernetesClientSparkSettings
{
    /// <summary>
    ///     <inheritdoc cref="HealthCheckSettings" />
    /// </summary>
    public HealthCheckSettings HealthChecks { get; set; } = new();
}