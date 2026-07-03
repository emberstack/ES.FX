using ES.FX.Additions.KubernetesClient.HealthChecks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace ES.FX.Additions.KubernetesClient.Tests;

public class KubernetesHealthCheckTests
{
    private static HealthCheckContext CreateContext(
        HealthStatus failureStatus = HealthStatus.Unhealthy) =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "kubernetes",
                Mock.Of<IHealthCheck>(),
                failureStatus,
                tags: null)
        };

    /// <summary>
    ///     Builds an <see cref="IKubernetes" /> whose <c>Version.GetCodeAsync(...)</c> resolves to a
    ///     <see cref="VersionInfo" /> with the given <paramref name="gitVersion" />. The public
    ///     <c>GetCodeAsync</c> extension delegates to <c>GetCodeWithHttpMessagesAsync</c>, so that is what
    ///     we set up on the mocked <see cref="IVersionOperations" />.
    /// </summary>
    private static Mock<IKubernetes> CreateClientReturning(string? gitVersion)
    {
        var version = new Mock<IVersionOperations>();
        version
            .Setup(v => v.GetCodeWithHttpMessagesAsync(
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpOperationResponse<VersionInfo>
            {
                Body = gitVersion is null ? null! : new VersionInfo { GitVersion = gitVersion },
                Response = new HttpResponseMessage()
            });

        var client = new Mock<IKubernetes>();
        client.SetupGet(c => c.Version).Returns(version.Object);
        return client;
    }

    /// <summary>Builds an <see cref="IKubernetes" /> whose version call throws.</summary>
    private static Mock<IKubernetes> CreateClientThrowing(Exception exception)
    {
        var version = new Mock<IVersionOperations>();
        version
            .Setup(v => v.GetCodeWithHttpMessagesAsync(
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var client = new Mock<IKubernetes>();
        client.SetupGet(c => c.Version).Returns(version.Object);
        return client;
    }

    // ---- Healthy branch ---------------------------------------------------------

    [Fact]
    public async Task CheckHealthAsync_ValidGitVersion_ReturnsHealthy()
    {
        var client = CreateClientReturning("v1.30.2");
        var sut = new KubernetesHealthCheck(client.Object);

        var result = await sut.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Kubernetes cluster is reachable.", result.Description);
        Assert.Null(result.Exception);
    }

    // ---- Empty / null version branch -> FailureStatus ---------------------------

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CheckHealthAsync_EmptyOrNullGitVersion_ReturnsFailureStatus(string? gitVersion)
    {
        var client = CreateClientReturning(gitVersion);
        var sut = new KubernetesHealthCheck(client.Object);

        var result = await sut.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Failed to retrieve valid version info.", result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_NullBody_ReturnsFailureStatus()
    {
        // Body itself is null (versionInfo?.GitVersion => null path).
        var client = CreateClientReturning(null);
        var sut = new KubernetesHealthCheck(client.Object);

        var result = await sut.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Failed to retrieve valid version info.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_FailureStatusHonorsRegistration()
    {
        // The failure status must come from the registration, not a hard-coded Unhealthy.
        var client = CreateClientReturning("");
        var sut = new KubernetesHealthCheck(client.Object);

        var result = await sut.CheckHealthAsync(
            CreateContext(HealthStatus.Degraded), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    // ---- Generic exception branch -> FailureStatus + exception attached ---------

    [Fact]
    public async Task CheckHealthAsync_GenericException_ReturnsFailureStatusWithExceptionAndDoesNotRethrow()
    {
        var boom = new InvalidOperationException("connection refused");
        var client = CreateClientThrowing(boom);
        var sut = new KubernetesHealthCheck(client.Object);

        var result = await sut.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Error checking Kubernetes cluster connectivity.", result.Description);
        Assert.Same(boom, result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_GenericException_HonorsRegistrationFailureStatus()
    {
        var client = CreateClientThrowing(new HttpOperationException("nope"));
        var sut = new KubernetesHealthCheck(client.Object);

        var result = await sut.CheckHealthAsync(
            CreateContext(HealthStatus.Degraded), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_OperationCanceledWithoutCancellationRequested_IsTreatedAsGenericFailure()
    {
        // OperationCanceledException but the token was NOT cancelled: the catch filter is false,
        // so it must fall through to the generic handler and NOT rethrow.
        var oce = new OperationCanceledException("aborted internally");
        var client = CreateClientThrowing(oce);
        var sut = new KubernetesHealthCheck(client.Object);

        var result = await sut.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Error checking Kubernetes cluster connectivity.", result.Description);
        Assert.Same(oce, result.Exception);
    }

    // ---- Cancellation branch -> rethrow -----------------------------------------

    [Fact]
    public async Task CheckHealthAsync_CancelledTokenAndOperationCanceled_Rethrows()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var client = CreateClientThrowing(new OperationCanceledException(cts.Token));
        var sut = new KubernetesHealthCheck(client.Object);

        // The catch filter `when (cancellationToken.IsCancellationRequested)` must rethrow,
        // NOT swallow into an Unhealthy result.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.CheckHealthAsync(CreateContext(), cts.Token));
    }

    [Fact]
    public async Task CheckHealthAsync_CancelledToken_PropagatesTokenToClient()
    {
        // Confirms the caller's CancellationToken is threaded into the version call.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var version = new Mock<IVersionOperations>();
        version
            .Setup(v => v.GetCodeWithHttpMessagesAsync(
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                It.Is<CancellationToken>(t => t.IsCancellationRequested)))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var client = new Mock<IKubernetes>();
        client.SetupGet(c => c.Version).Returns(version.Object);
        var sut = new KubernetesHealthCheck(client.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.CheckHealthAsync(CreateContext(), cts.Token));

        version.Verify(
            v => v.GetCodeWithHttpMessagesAsync(
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(),
                It.Is<CancellationToken>(t => t.IsCancellationRequested)),
            Times.Once);
    }
}
