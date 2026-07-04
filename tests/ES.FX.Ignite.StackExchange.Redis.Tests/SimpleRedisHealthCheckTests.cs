using System.Net;
using ES.FX.Ignite.StackExchange.Redis.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Tests;

/// <summary>
///     Fast, deterministic coverage of <see cref="SimpleRedisHealthCheck" /> using mocked
///     <see cref="IConnectionMultiplexer" /> collaborators. Exercises the healthy path, the
///     failure-status mapping, the cluster branch (ok / not-ok / null) and cancellation.
/// </summary>
public class SimpleRedisHealthCheckTests
{
    private static readonly EndPoint EndPoint = new IPEndPoint(IPAddress.Loopback, 6379);

    // SimpleRedisHealthCheck is internal; construct it via reflection so the test project does not
    // need InternalsVisibleTo.
    private static IHealthCheck CreateHealthCheck(IConnectionMultiplexer multiplexer)
    {
        var type = typeof(RedisSpark).Assembly.GetType(
            "ES.FX.Ignite.StackExchange.Redis.HealthChecks.SimpleRedisHealthCheck", true)!;
        return (IHealthCheck)Activator.CreateInstance(type, multiplexer)!;
    }

    private static HealthCheckContext CreateContext(
        HealthStatus failureStatus = HealthStatus.Unhealthy) =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "Redis",
                Mock.Of<IHealthCheck>(),
                failureStatus,
                null)
        };

    private static Mock<IConnectionMultiplexer> CreateMultiplexer(
        ServerType serverType,
        Mock<IServer> server,
        Mock<IDatabase>? database = null)
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetEndPoints(It.IsAny<bool>())).Returns([EndPoint]);
        multiplexer.Setup(m => m.GetServer(EndPoint, It.IsAny<object?>())).Returns(server.Object);
        server.SetupGet(s => s.ServerType).Returns(serverType);

        database ??= new Mock<IDatabase>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(database.Object);
        return multiplexer;
    }

    [Fact]
    public async Task Healthy_When_Standalone_Pings_Succeed()
    {
        var server = new Mock<IServer>();
        server.Setup(s => s.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.FromMilliseconds(1));

        var database = new Mock<IDatabase>();
        database.Setup(d => d.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.FromMilliseconds(1));

        var multiplexer = CreateMultiplexer(ServerType.Standalone, server, database);
        var sut = CreateHealthCheck(multiplexer.Object);

        var result = await sut.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        database.Verify(d => d.PingAsync(It.IsAny<CommandFlags>()), Times.Once);
        server.Verify(s => s.PingAsync(It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task Unhealthy_And_ExceptionCaptured_When_Ping_Throws()
    {
        var boom = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "boom");
        var server = new Mock<IServer>();
        server.SetupGet(s => s.ServerType).Returns(ServerType.Standalone);

        var database = new Mock<IDatabase>();
        database.Setup(d => d.PingAsync(It.IsAny<CommandFlags>())).ThrowsAsync(boom);

        var multiplexer = CreateMultiplexer(ServerType.Standalone, server, database);
        var sut = CreateHealthCheck(multiplexer.Object);

        var result = await sut.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Same(boom, result.Exception);
    }

    [Fact]
    public async Task Uses_Registration_FailureStatus_On_Error()
    {
        var server = new Mock<IServer>();
        server.SetupGet(s => s.ServerType).Returns(ServerType.Standalone);

        var database = new Mock<IDatabase>();
        database.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new InvalidOperationException("nope"));

        var multiplexer = CreateMultiplexer(ServerType.Standalone, server, database);
        var sut = CreateHealthCheck(multiplexer.Object);

        var result =
            await sut.CheckHealthAsync(CreateContext(HealthStatus.Degraded), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task Cluster_Healthy_When_State_Ok()
    {
        var server = new Mock<IServer>();
        server.Setup(s => s.ExecuteAsync("CLUSTER", It.IsAny<object[]>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)"cluster_state:ok\r\ncluster_slots_assigned:16384"));

        var multiplexer = CreateMultiplexer(ServerType.Cluster, server);
        var sut = CreateHealthCheck(multiplexer.Object);

        var result = await sut.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        // Cluster branch must NOT ping the database/server directly.
        server.Verify(s => s.ExecuteAsync("CLUSTER", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task Cluster_Unhealthy_When_State_Not_Ok()
    {
        var server = new Mock<IServer>();
        server.Setup(s => s.ExecuteAsync("CLUSTER", It.IsAny<object[]>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)"cluster_state:fail"));

        var multiplexer = CreateMultiplexer(ServerType.Cluster, server);
        var sut = CreateHealthCheck(multiplexer.Object);

        var result = await sut.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not on OK state", result.Description);
    }

    [Fact]
    public async Task Cluster_Unhealthy_When_Result_Null()
    {
        var server = new Mock<IServer>();
        server.Setup(s => s.ExecuteAsync("CLUSTER", It.IsAny<object[]>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null));

        var multiplexer = CreateMultiplexer(ServerType.Cluster, server);
        var sut = CreateHealthCheck(multiplexer.Object);

        var result = await sut.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("null or can't be read", result.Description);
    }

    [Fact]
    public async Task Cancellation_Requested_Before_Ping_Yields_FailureStatus()
    {
        var server = new Mock<IServer>();
        server.SetupGet(s => s.ServerType).Returns(ServerType.Standalone);
        var multiplexer = CreateMultiplexer(ServerType.Standalone, server);
        var sut = CreateHealthCheck(multiplexer.Object);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // The loop's ThrowIfCancellationRequested throws OperationCanceledException, which the
        // catch block maps to the registration FailureStatus rather than propagating.
        var result = await sut.CheckHealthAsync(CreateContext(), cts.Token);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.IsAssignableFrom<OperationCanceledException>(result.Exception);
    }
}