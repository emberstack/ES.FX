namespace ES.FX.Hosting.Tests.SUT;

/// <summary>
///     Console markers shared between the SUT and the tests that drive it.
/// </summary>
public static class SutMarkers
{
    /// <summary>
    ///     Written once the entry point is running, i.e. after its POSIX signal handlers are registered.
    /// </summary>
    public const string Ready = "ES.FX.Hosting.Tests.SUT ready";
}
