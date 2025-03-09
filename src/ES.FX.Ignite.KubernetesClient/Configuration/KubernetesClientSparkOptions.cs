using k8s;

namespace ES.FX.Ignite.KubernetesClient.Configuration;

/// <summary>
///     Provides the options for connecting to a Kubernetes cluster using a <see cref="IKubernetes" />
/// </summary>
public class KubernetesClientSparkOptions
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the TLS verification is skipped.
    /// </summary>
    public bool SkipTlsVerify { get; set; }
}