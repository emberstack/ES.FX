using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Azure.Storage.Blobs.HealthChecks;

/// <summary>
///     Azure Blob Storage health check.
/// </summary>
/// <remarks>
///     Uses a page-list probe (page size 1) instead of <c>BlobServiceClient.GetPropertiesAsync</c> so the check
///     works with the least-privileged role assignment ("Storage Blob Data Reader" at storage account level).
///     <c>GetPropertiesAsync</c> requires elevated permissions that "Storage Blob Data Contributor" does not grant.
/// </remarks>
internal sealed class SimpleBlobServiceHealthCheck(BlobServiceClient blobServiceClient) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await blobServiceClient
                .GetBlobContainersAsync(cancellationToken: cancellationToken)
                .AsPages(pageSizeHint: 1)
                .GetAsyncEnumerator(cancellationToken)
                .MoveNextAsync()
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
