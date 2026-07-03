using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace ES.FX.Ignite.Spark.Tests;

/// <summary>
///     Additional coverage for <see cref="KeyedConfigurationGuard" /> branches not exercised by
///     <see cref="SparkGuardTests" />: the default-message path (no explicit message supplied) and the
///     argument guard clauses.
/// </summary>
public class KeyedConfigurationGuardCoverageTests
{
    private static IHostApplicationBuilder BuilderWithProperties()
    {
        var builderMock = new Mock<IHostApplicationBuilder>();
        builderMock.Setup(mock => mock.Properties)
            .Returns(new Dictionary<object, object>());
        return builderMock.Object;
    }

    [Fact]
    public void GuardConfigurationKey_DefaultMessage_UsesAlreadyConfiguredError()
    {
        const string key = "spark";
        var builder = BuilderWithProperties();

        // First call sets the guard (no message supplied).
        builder.GuardConfigurationKey(key);

        // Second call with no message must fall back to AlreadyConfiguredError(key).
        var exception = Assert.Throws<ReconfigurationNotSupportedException>(() =>
            builder.GuardConfigurationKey(key));

        Assert.Equal(KeyedConfigurationGuard.AlreadyConfiguredError(key), exception.Message);
    }

    [Fact]
    public void GuardConfigurationKey_EmptyMessage_FallsBackToDefaultMessage()
    {
        const string key = "spark";
        var builder = BuilderWithProperties();

        builder.GuardConfigurationKey(key);

        // string.IsNullOrEmpty(message) is true for empty, so the default message is used.
        var exception = Assert.Throws<ReconfigurationNotSupportedException>(() =>
            builder.GuardConfigurationKey(key, string.Empty));

        Assert.Equal(KeyedConfigurationGuard.AlreadyConfiguredError(key), exception.Message);
    }

    [Fact]
    public void GuardConfigurationKey_ExplicitMessage_UsesThatMessage()
    {
        const string key = "spark";
        const string customMessage = "custom reconfiguration message";
        var builder = BuilderWithProperties();

        builder.GuardConfigurationKey(key);

        var exception = Assert.Throws<ReconfigurationNotSupportedException>(() =>
            builder.GuardConfigurationKey(key, customMessage));

        Assert.Equal(customMessage, exception.Message);
    }

    [Fact]
    public void ReconfigurationNotSupportedException_IsNotSupportedException()
    {
        var exception = new ReconfigurationNotSupportedException("msg");

        Assert.IsAssignableFrom<NotSupportedException>(exception);
        Assert.Equal("msg", exception.Message);
    }

    // ---- Guard clauses ----

    [Fact]
    public void GuardConfigurationKey_NullBuilder_Throws()
    {
        IHostApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.GuardConfigurationKey("key"));
    }

    [Fact]
    public void GuardConfigurationKey_NullKey_Throws()
    {
        var builder = BuilderWithProperties();

        Assert.Throws<ArgumentNullException>(() => builder.GuardConfigurationKey(null!));
    }

    [Fact]
    public void GuardConfigurationKey_EmptyKey_Throws()
    {
        var builder = BuilderWithProperties();

        Assert.Throws<ArgumentException>(() => builder.GuardConfigurationKey(string.Empty));
    }

    [Fact]
    public void IsGuardConfigurationKeySet_NullBuilder_Throws()
    {
        IHostApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.IsGuardConfigurationKeySet("key"));
    }

    [Fact]
    public void IsGuardConfigurationKeySet_NullKey_Throws()
    {
        var builder = BuilderWithProperties();

        Assert.Throws<ArgumentNullException>(() => builder.IsGuardConfigurationKeySet(null!));
    }

    [Fact]
    public void IsGuardConfigurationKeySet_EmptyKey_Throws()
    {
        var builder = BuilderWithProperties();

        Assert.Throws<ArgumentException>(() => builder.IsGuardConfigurationKeySet(string.Empty));
    }

    [Fact]
    public void IsGuardConfigurationKeySet_ReturnsFalse_WhenNotSet()
    {
        var builder = BuilderWithProperties();

        Assert.False(builder.IsGuardConfigurationKeySet("never-set"));
    }
}
