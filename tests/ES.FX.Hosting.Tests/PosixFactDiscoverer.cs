using Xunit.Sdk;
using Xunit.v3;

namespace ES.FX.Hosting.Tests;

/// <summary>
///     Discovers <see cref="PosixFactAttribute" /> tests: identical to <see cref="FactDiscoverer" />, except that it
///     yields no test cases on Windows.
/// </summary>
public sealed class PosixFactDiscoverer : FactDiscoverer
{
    /// <inheritdoc />
    public override ValueTask<IReadOnlyCollection<IXunitTestCase>> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions, IXunitTestMethod testMethod, IFactAttribute factAttribute) =>
        OperatingSystem.IsWindows()
            ? new ValueTask<IReadOnlyCollection<IXunitTestCase>>([])
            : base.Discover(discoveryOptions, testMethod, factAttribute);
}
