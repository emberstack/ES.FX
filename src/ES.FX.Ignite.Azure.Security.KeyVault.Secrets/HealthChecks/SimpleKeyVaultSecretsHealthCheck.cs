using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Azure.Security.KeyVault.Secrets.HealthChecks;

/// <summary>
///     Azure Key Vault secrets health check.
/// </summary>
/// <remarks>
///     Probes the vault by issuing <see cref="SecretClient.GetSecretAsync(string, string, CancellationToken)" />
///     against a sentinel secret name. The check only requires "Get" permission on that one secret name
///     (no List permission needed). A 404 response is treated as healthy because the connection succeeded —
///     the absence of the sentinel secret is intentional and does not require it to exist.
/// </remarks>
internal sealed class SimpleKeyVaultSecretsHealthCheck(SecretClient secretClient) : IHealthCheck
{
    private const string ProbeSecretName = "AzureKeyVaultSecretsHealthCheck";

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await secretClient.GetSecretAsync(ProbeSecretName, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}