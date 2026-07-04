namespace ES.FX.Additions.Serilog.Tests;

/// <summary>
///     Groups tests that mutate global Serilog state (<c>Serilog.Log.Logger</c> / <c>SelfLog</c>) so they never run in
///     parallel with one another and clobber each other's global logger.
/// </summary>
[CollectionDefinition(nameof(SerilogGlobalStateCollection), DisableParallelization = true)]
public sealed class SerilogGlobalStateCollection;