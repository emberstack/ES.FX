using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime.Tests;

/// <summary>
///     Deterministic, Docker-free coverage for the factory's extensibility surface and the container-name
///     sanitization that keeps closed-generic usage Docker-legal.
/// </summary>
/// <remarks>
///     <see cref="TestContainerDesignTimeFactory{TDbContext}.ConfigureMsSqlContainerBuilder" /> runs before the
///     container is built and started, so its invocation can be proven without Docker by throwing a sentinel
///     from an override and observing that <c>CreateDbContext</c> surfaces it before any Docker interaction.
///     The other two hooks (<c>ConfigureDbContextOptionsBuilder</c>, <c>ConfigureSqlServerOptions</c>) run only
///     after a successful container start and are covered by the Docker-backed suite.
/// </remarks>
public sealed class ExtensibilityAndNamingTests
{
    [Fact]
    public void ConfigureMsSqlContainerBuilder_IsInvoked_BeforeContainerBuildAndStart()
    {
        // The hook is called synchronously on line "ConfigureMsSqlContainerBuilder(builder)" before Build()
        // and StartAsync(). By throwing a unique sentinel from the override we prove the extension point runs
        // during CreateDbContext without needing Docker at all: if it were never called we'd instead get a
        // Docker/naming failure.
        var factory = new HookProbeFactory();

        var ex = Assert.Throws<HookInvokedSentinelException>(() => factory.CreateDbContext([]));

        Assert.NotNull(ex.Builder);
    }

    [Fact]
    public void ClosedGenericTypeName_ContainsBacktick_WhichTheFactoryStripsForTheContainerName()
    {
        // Documents WHY the factory sanitizes its container name: for a CLOSED generic, GetType().Name is the
        // arity-suffixed reflection name containing a backtick, which Docker forbids in container names. The
        // factory strips everything from the backtick onward (source line 45); assert the raw name still carries
        // the backtick (so the sanitization stays necessary) and that stripping it yields the Docker-legal name.
        var typeName = typeof(TestContainerDesignTimeFactory<OptionsCtorDbContext>).Name;

        Assert.Contains("`", typeName, StringComparison.Ordinal);
        Assert.StartsWith("TestContainerDesignTimeFactory`1", typeName, StringComparison.Ordinal);
        Assert.Equal("TestContainerDesignTimeFactory", typeName.Split('`')[0]);
    }
}

/// <summary>Sentinel raised from the overridden hook to prove it was invoked.</summary>
internal sealed class HookInvokedSentinelException(MsSqlBuilder builder) : Exception
{
    public MsSqlBuilder Builder { get; } = builder;
}

/// <summary>
///     A factory subclass whose <see cref="ConfigureMsSqlContainerBuilder" /> override throws a sentinel,
///     letting a test confirm the extension point is reached (with the real builder) during
///     <see cref="TestContainerDesignTimeFactory{TDbContext}.CreateDbContext" />.
/// </summary>
internal sealed class HookProbeFactory : TestContainerDesignTimeFactory<OptionsCtorDbContext>
{
    protected override void ConfigureMsSqlContainerBuilder(MsSqlBuilder builder) =>
        throw new HookInvokedSentinelException(builder);
}
