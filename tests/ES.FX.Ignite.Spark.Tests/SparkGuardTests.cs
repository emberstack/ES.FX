using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace ES.FX.Ignite.Spark.Tests;

public class SparkGuardTests
{
    [Theory]
    [InlineData("")]
    [InlineData("key1")]
    public void DefaultErrorMessageGeneratorTest(string testKey)
    {
        Assert.False(string.IsNullOrEmpty(SparkGuard.DefaultConfigurationErrorMessageGenerator(testKey)));
    }

    [Fact]
    public void SparkGuardFirstConfigure()
    {
        var builderMock = new Mock<IHostApplicationBuilder>();
        var key = "key1";
        builderMock.Setup(builderMock => builderMock.Properties).Returns(new Dictionary<object, object>()).Verifiable();

        var message = SparkGuard.DefaultConfigurationErrorMessageGenerator(key);

        SparkGuard.GuardSparkConfiguration(builderMock.Object, key, message);

        Assert.True(builderMock.Object.Properties.TryGetValue($"{nameof(SparkGuard)}-{key}", out var value));
    }

    [Fact]
    public void SparkGuardReconfigurationNotSupported()
    {
        var builderMock = new Mock<IHostApplicationBuilder>();
        var key = "key1";
        builderMock.Setup(builderMock => builderMock.Properties).Returns(new Dictionary<object, object>() { { key, string.Empty } }).Verifiable();

        var message = SparkGuard.DefaultConfigurationErrorMessageGenerator(key);

        SparkGuard.GuardSparkConfiguration(builderMock.Object, key, message);

        Assert.Throws<SparkReconfigurationNotSupportedException>(() => SparkGuard.GuardSparkConfiguration(builderMock.Object, key, message));
    }

}