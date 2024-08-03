using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace ES.FX.Ignite.Spark.Tests;

public class SparkGuardTests
{
    [Theory]
    [InlineData("")]
    [InlineData("key1")]
    public void AlreadyConfiguredError_NotEmpty(string testKey)
    {
        Assert.False(string.IsNullOrEmpty(KeyedConfigurationGuard.AlreadyConfiguredError(testKey)));
    }

    [Fact]
    public void Guard_Configuration()
    {
        const string key = "spark";

        var builderMock = new Mock<IHostApplicationBuilder>();
        builderMock.Setup(mock => mock.Properties)
            .Returns(new Dictionary<object, object>()).Verifiable();

        var message = KeyedConfigurationGuard.AlreadyConfiguredError(key);

        builderMock.Object.GuardConfigurationKey(key, message);

        Assert.True(builderMock.Object.ConfigurationKeyGuardSet(key));
    }

    [Fact]
    public void Guard_ReconfigurationNotSupported()
    {
        const string key = "spark";

        var builderMock = new Mock<IHostApplicationBuilder>();
        builderMock.Setup(mock => mock.Properties)
            .Returns(new Dictionary<object, object>()).Verifiable();

        var message = KeyedConfigurationGuard.AlreadyConfiguredError(key);

        builderMock.Object.GuardConfigurationKey(key, message);

        Assert.Throws<ReconfigurationNotSupportedException>(() =>
            builderMock.Object.GuardConfigurationKey(key, message));
    }
}