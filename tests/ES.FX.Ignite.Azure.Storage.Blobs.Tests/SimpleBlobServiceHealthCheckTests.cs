using System.Reflection;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using static Xunit.Assert;

namespace ES.FX.Ignite.Azure.Storage.Blobs.Tests;

/// <summary>
///     Functional coverage of <c>SimpleBlobServiceHealthCheck.CheckHealthAsync</c>.
///     The type is <c>internal</c>, so it is constructed via reflection and driven through the public
///     <see cref="IHealthCheck" /> interface. The collaborating <see cref="BlobServiceClient" /> is a Moq
///     mock, so no live Azure / Docker is required — the page-list probe branches are exercised directly.
/// </summary>
public class SimpleBlobServiceHealthCheckTests
{
    private const string HealthCheckTypeName =
        "ES.FX.Ignite.Azure.Storage.Blobs.HealthChecks.SimpleBlobServiceHealthCheck";

    private static IHealthCheck CreateHealthCheck(BlobServiceClient client)
    {
        var type = typeof(AzureBlobStorageSpark).Assembly.GetType(HealthCheckTypeName, true)!;
        var instance = Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            [client],
            null);
        return (IHealthCheck)instance!;
    }

    private static HealthCheckContext ContextWithFailureStatus(HealthStatus failureStatus) => new()
    {
        Registration = new HealthCheckRegistration(
            "blob",
            Mock.Of<IHealthCheck>(),
            failureStatus,
            null)
    };

    [Fact]
    public async Task CheckHealthAsync_WhenPageListSucceeds_ReturnsHealthy()
    {
        // A reachable service: the page-list probe yields a page (which the check breaks out of after the
        // first page). This is the "Healthy" branch without needing a real blob endpoint.
        var page = Page<BlobContainerItem>.FromValues(
            [],
            null,
            Mock.Of<Response>());
        var pageable = AsyncPageable<BlobContainerItem>.FromPages([page]);

        var client = new Mock<BlobServiceClient>();
        client
            .Setup(c => c.GetBlobContainersAsync(
                It.IsAny<BlobContainerTraits>(),
                It.IsAny<BlobContainerStates>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(pageable);

        var healthCheck = CreateHealthCheck(client.Object);

        var result = await healthCheck.CheckHealthAsync(
            ContextWithFailureStatus(HealthStatus.Unhealthy),
            TestContext.Current.CancellationToken);

        Equal(HealthStatus.Healthy, result.Status);
        Null(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPageListThrows_ReturnsFailureStatusWithException()
    {
        // An unreachable / unauthorized service: the probe throws. The check must NOT let the exception
        // escape; it must return the registration's FailureStatus and capture the exception.
        var boom = new RequestFailedException(403, "Forbidden");

        var client = new Mock<BlobServiceClient>();
        client
            .Setup(c => c.GetBlobContainersAsync(
                It.IsAny<BlobContainerTraits>(),
                It.IsAny<BlobContainerStates>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Throws(boom);

        var healthCheck = CreateHealthCheck(client.Object);

        var result = await healthCheck.CheckHealthAsync(
            ContextWithFailureStatus(HealthStatus.Degraded),
            TestContext.Current.CancellationToken);

        Equal(HealthStatus.Degraded, result.Status);
        Same(boom, result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPageEnumerationThrows_ReturnsFailureStatusWithException()
    {
        // The probe enumerates pages lazily. Simulate a failure surfaced while awaiting the first page
        // (rather than at the synchronous call) to prove the try/catch wraps the enumeration too.
        var boom = new RequestFailedException(500, "Server error");

        var client = new Mock<BlobServiceClient>();
        client
            .Setup(c => c.GetBlobContainersAsync(
                It.IsAny<BlobContainerTraits>(),
                It.IsAny<BlobContainerStates>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ThrowingPageable(boom));

        var healthCheck = CreateHealthCheck(client.Object);

        var result = await healthCheck.CheckHealthAsync(
            ContextWithFailureStatus(HealthStatus.Unhealthy),
            TestContext.Current.CancellationToken);

        Equal(HealthStatus.Unhealthy, result.Status);
        Same(boom, result.Exception);
    }

    private static AsyncPageable<BlobContainerItem> ThrowingPageable(Exception exception) =>
        new ThrowingAsyncPageable(exception);

    private sealed class ThrowingAsyncPageable(Exception exception) : AsyncPageable<BlobContainerItem>
    {
        public override async IAsyncEnumerable<Page<BlobContainerItem>> AsPages(
            string? continuationToken = null,
            int? pageSizeHint = null)
        {
            await Task.Yield();
            throw exception;
#pragma warning disable CS0162 // Unreachable code — required so the compiler treats this as an iterator.
            yield break;
#pragma warning restore CS0162
        }
    }
}