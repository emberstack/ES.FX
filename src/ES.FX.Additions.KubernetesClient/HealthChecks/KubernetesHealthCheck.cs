using JetBrains.Annotations;
using k8s;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Additions.KubernetesClient.HealthChecks;

/// <summary>
///     Health check that verifies connectivity to the Kubernetes cluster
/// </summary>
/// <param name="client">The <see cref="IKubernetes" /> client used to contact the cluster</param>
[PublicAPI]
public class KubernetesHealthCheck(IKubernetes client) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // The version endpoint is generally accessible without special permissions.
            var versionInfo = await client.Version.GetCodeAsync(cancellationToken).ConfigureAwait(false);
            return !string.IsNullOrEmpty(versionInfo?.GitVersion)
                ? HealthCheckResult.Healthy("Kubernetes cluster is reachable.")
                : new HealthCheckResult(context.Registration.FailureStatus,
                    "Failed to retrieve valid version info.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Any exception indicates a problem connecting to the cluster.
            return new HealthCheckResult(context.Registration.FailureStatus,
                "Error checking Kubernetes cluster connectivity.", ex);
        }
    }
}