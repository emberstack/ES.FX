using JetBrains.Annotations;
using k8s;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Additions.KubernetesClient.HealthChecks;

[PublicAPI]
public class KubernetesHealthCheck(IKubernetes client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // The version endpoint is generally accessible without special permissions.
            var versionInfo = await client.Version.GetCodeAsync(cancellationToken);
            return !string.IsNullOrEmpty(versionInfo?.GitVersion)
                ? HealthCheckResult.Healthy("Kubernetes cluster is reachable.")
                : HealthCheckResult.Unhealthy("Failed to retrieve valid version info.");
        }
        catch (Exception ex)
        {
            // Any exception indicates a problem connecting to the cluster.
            return HealthCheckResult.Unhealthy("Error checking Kubernetes cluster connectivity.", ex);
        }
    }
}