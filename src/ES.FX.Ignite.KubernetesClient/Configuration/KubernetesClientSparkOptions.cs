using k8s;

namespace ES.FX.Ignite.KubernetesClient.Configuration;

/// <summary>
///     Provides the options for connecting to a Kubernetes cluster using a <see cref="IKubernetes" />
/// </summary>
public class KubernetesClientSparkOptions
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether the TLS verification is skipped.
    ///     When <see langword="null" /> (the default), the value resolved from the
    ///     <see cref="KubernetesClientConfiguration" /> (for example a kubeconfig's
    ///     <c>insecure-skip-tls-verify</c> flag) is left untouched.
    /// </summary>
    public bool? SkipTlsVerify { get; set; }
}