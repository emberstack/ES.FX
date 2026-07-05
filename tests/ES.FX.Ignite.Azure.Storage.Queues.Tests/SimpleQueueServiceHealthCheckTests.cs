using System.Reflection;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using static Xunit.Assert;

namespace ES.FX.Ignite.Azure.Storage.Queues.Tests;

/// <summary>
///     Functional coverage of the shipped (<c>internal</c>) <c>SimpleQueueServiceHealthCheck.CheckHealthAsync</c>.
///     The type is constructed via reflection and driven through the public <see cref="IHealthCheck" /> interface.
///     The collaborating <see cref="QueueServiceClient" /> is a Moq mock, so no live Azure / Docker / Azurite is
///     required — the real page-list probe branches are exercised directly.
///     These tests exist because the only pre-existing runtime health-check test drove exclusively the catch/failure
///     branch (it pointed at a non-resolvable account), leaving the success path, the "break after first page"
///     behavior, and the deliberate <c>GetQueuesAsync</c> (least-privilege) probe strategy entirely unverified.
/// </summary>
public class SimpleQueueServiceHealthCheckTests
{
    private const string HealthCheckTypeName =
        "ES.FX.Ignite.Azure.Storage.Queues.HealthChecks.SimpleQueueServiceHealthCheck";

    private static IHealthCheck CreateHealthCheck(QueueServiceClient client)
    {
        var type = typeof(AzureQueueStorageSpark).Assembly.GetType(HealthCheckTypeName, true)!;
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
            "queue",
            Mock.Of<IHealthCheck>(),
            failureStatus,
            null)
    };

    private static QueueItem SampleQueue(string name) =>
        QueuesModelFactory.QueueItem(name);

    /// <summary>
    ///     Healthy branch: the page-list probe yields a page and the check breaks out after the first one.
    ///     Kills the mutation that flips the success return from <c>Healthy()</c> to <c>Unhealthy()/Degraded()</c>.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_WhenPageListSucceeds_ReturnsHealthy()
    {
        var page = Page<QueueItem>.FromValues(
            [SampleQueue("q1")],
            null,
            Mock.Of<Response>());
        var pageable = AsyncPageable<QueueItem>.FromPages([page]);

        var client = new Mock<QueueServiceClient>();
        client
            .Setup(c => c.GetQueuesAsync(
                It.IsAny<QueueTraits>(),
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

    /// <summary>
    ///     Probe-strategy guard: the check must use the least-privilege <c>GetQueuesAsync</c> page-list probe, NOT
    ///     <c>GetPropertiesAsync</c> (which requires elevated permissions — the documented raison d'être of this class).
    ///     If the implementation were reverted to a <c>GetPropertiesAsync</c> probe, that member is wired to throw here,
    ///     so the check would report the failure status instead of Healthy and this test would fail.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_UsesGetQueuesProbe_NotGetProperties()
    {
        var page = Page<QueueItem>.FromValues(
            [SampleQueue("q1")],
            null,
            Mock.Of<Response>());
        var pageable = AsyncPageable<QueueItem>.FromPages([page]);

        var client = new Mock<QueueServiceClient>(MockBehavior.Strict);
        client
            .Setup(c => c.GetQueuesAsync(
                It.IsAny<QueueTraits>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(pageable);
        // Reverting to the elevated-permission probe must break the check: make it blow up if ever called.
        client
            .Setup(c => c.GetPropertiesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "GetPropertiesAsync requires elevated permissions"));

        var healthCheck = CreateHealthCheck(client.Object);

        var result = await healthCheck.CheckHealthAsync(
            ContextWithFailureStatus(HealthStatus.Unhealthy),
            TestContext.Current.CancellationToken);

        Equal(HealthStatus.Healthy, result.Status);
        Null(result.Exception);
        client.Verify(c => c.GetQueuesAsync(
            It.IsAny<QueueTraits>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Break guard: the check must stop after the FIRST page. The probe returns a pageable whose second page
    ///     throws; a correct implementation breaks before touching it and returns Healthy. If the <c>break</c> were
    ///     removed (full enumeration), the second page would throw and the check would report the failure status.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_BreaksAfterFirstPage_DoesNotEnumerateRemainingPages()
    {
        var boom = new RequestFailedException(500, "second page must never be enumerated");
        var pageable = new SecondPageThrowsAsyncPageable(SampleQueue("q1"), boom);

        var client = new Mock<QueueServiceClient>();
        client
            .Setup(c => c.GetQueuesAsync(
                It.IsAny<QueueTraits>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(pageable);

        var healthCheck = CreateHealthCheck(client.Object);

        var result = await healthCheck.CheckHealthAsync(
            ContextWithFailureStatus(HealthStatus.Unhealthy),
            TestContext.Current.CancellationToken);

        Equal(HealthStatus.Healthy, result.Status);
        Null(result.Exception);
        // Prove the guard actually did its job: exactly one page was pulled.
        Equal(1, pageable.PagesEnumerated);
    }

    /// <summary>
    ///     Failure branch (synchronous throw): an unreachable / unauthorized service where the probe throws when
    ///     invoked. The check must NOT let the exception escape; it returns the registration's FailureStatus and
    ///     captures the exact exception instance.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_WhenProbeThrows_ReturnsFailureStatusWithSameException()
    {
        var boom = new RequestFailedException(403, "Forbidden");

        var client = new Mock<QueueServiceClient>();
        client
            .Setup(c => c.GetQueuesAsync(
                It.IsAny<QueueTraits>(),
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

    /// <summary>
    ///     Failure branch (lazy enumeration throw): the probe enumerates pages lazily; the failure surfaces while
    ///     awaiting the first page rather than at the synchronous call. Proves the try/catch wraps the enumeration too.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_WhenPageEnumerationThrows_ReturnsFailureStatusWithSameException()
    {
        var boom = new RequestFailedException(500, "Server error");

        var client = new Mock<QueueServiceClient>();
        client
            .Setup(c => c.GetQueuesAsync(
                It.IsAny<QueueTraits>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ThrowingAsyncPageable(boom));

        var healthCheck = CreateHealthCheck(client.Object);

        var result = await healthCheck.CheckHealthAsync(
            ContextWithFailureStatus(HealthStatus.Unhealthy),
            TestContext.Current.CancellationToken);

        Equal(HealthStatus.Unhealthy, result.Status);
        Same(boom, result.Exception);
    }

    private sealed class ThrowingAsyncPageable(Exception exception) : AsyncPageable<QueueItem>
    {
        public override async IAsyncEnumerable<Page<QueueItem>> AsPages(
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

    /// <summary>
    ///     A pageable whose first page succeeds and whose second page throws. Records how many pages were pulled so
    ///     tests can assert the health check breaks after the first page instead of enumerating everything.
    /// </summary>
    private sealed class SecondPageThrowsAsyncPageable(QueueItem firstPageItem, Exception secondPageException)
        : AsyncPageable<QueueItem>
    {
        public int PagesEnumerated { get; private set; }

        public override async IAsyncEnumerable<Page<QueueItem>> AsPages(
            string? continuationToken = null,
            int? pageSizeHint = null)
        {
            await Task.Yield();

            PagesEnumerated++;
            yield return Page<QueueItem>.FromValues(
                [firstPageItem],
                "next",
                Mock.Of<Response>());

            PagesEnumerated++;
            throw secondPageException;
        }
    }
}