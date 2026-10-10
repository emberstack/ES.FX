using System.Runtime.CompilerServices;
using Xunit.v3;

namespace ES.FX.Hosting.Tests;

/// <summary>
///     A <see cref="FactAttribute" /> for tests that need POSIX signals. On Windows the test is not discovered at
///     all (instead of being reported as skipped); on Linux and macOS it runs like any other fact.
/// </summary>
[XunitTestCaseDiscoverer(typeof(PosixFactDiscoverer))]
public sealed class PosixFactAttribute(
    [CallerFilePath] string? sourceFilePath = null,
    [CallerLineNumber] int sourceLineNumber = -1)
    : FactAttribute(sourceFilePath, sourceLineNumber);
