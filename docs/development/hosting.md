---
title: Application hosting
description: Wrap your Main with ProgramEntry for structured startup logging, controlled exit codes, and guaranteed cleanup on shutdown.
---

## Overview

`ES.FX.Hosting` gives you a thin, dependency-light wrapper around your application's entry point. Instead of a bare `Main` that has to hand-roll try/catch, logging, and cleanup, you build a `ProgramEntry` and run your real startup logic inside it. In return you get:

- **Structured startup and shutdown logging** — the entry logs a `Trace` line when the program starts, a `Debug` line with the exit code it completed with, and a `Critical` log with the full exception if it terminates unexpectedly.
- **A stable exit code contract** — normal completion returns your code, a `ControlledExitException` returns its `ExitCode`, and any unhandled exception returns `1`.
- **Guaranteed cleanup** — exit actions always run in a `finally`, regardless of how the program exits (success, controlled exit, or crash).

This layer sits below Ignite and has no dependency on it. Use it in any host — a web app, a worker, or a minimal console tool — and reach for Ignite separately when you want the full observability bootstrap.

> [!NOTE]
> `ProgramEntry` does **not** configure Serilog. The `UseSerilog()` call you see in the playground entry points comes from [`ES.FX.Additions.Serilog`](../additions/serilog.md), not from Hosting. Out of the box `ProgramEntry` logs to a console `ILogger`.

## Install

```bash
dotnet add package ES.FX.Hosting
```

```xml
<PackageReference Include="ES.FX.Hosting" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so the in-repo `<PackageReference>` carries no `Version` attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

## Basic usage

Build a `ProgramEntry` from the command-line args and run your startup logic inside `RunAsync`. The delegate receives the `ProgramEntryOptions` and returns the process exit code as an `int`:

```csharp
using ES.FX.Hosting.Lifetime;

return await ProgramEntry.CreateBuilder(args).Build().RunAsync(async options =>
{
    var builder = Host.CreateApplicationBuilder(options.Args);
    // register services, configure the host …
    var app = builder.Build();
    await app.RunAsync();
    return 0;
});
```

`CreateBuilder(args)` returns a `ProgramEntryBuilder` preconfigured with a console logger. `Build()` produces the `ProgramEntry`, and `RunAsync` executes your delegate with the lifecycle guarantees described above.

> [!TIP]
> The top-level statement pattern above (a file whose first line is `return await …`) is the idiomatic entry point. Your `Program.cs` needs no explicit `Main`.

## API surface

Everything lives in the `ES.FX.Hosting.Lifetime` namespace.

| Member | Signature | Purpose |
| --- | --- | --- |
| `ProgramEntry.CreateBuilder` | `static ProgramEntryBuilder CreateBuilder(string[] args)` | Create a builder preconfigured with a console logger. |
| `ProgramEntry.RunAsync` | `Task<int> RunAsync(Func<ProgramEntryOptions, Task<int>> action)` | Run the startup delegate with logging, exit-code handling, and exit actions. |
| `ProgramEntryBuilder.WithLogger` | `ProgramEntryBuilder WithLogger(ILogger logger)` | Replace the default console logger. |
| `ProgramEntryBuilder.AddExitAction` | `ProgramEntryBuilder AddExitAction(Func<ProgramEntryOptions, Task> exitAction)` | Register a cleanup action that always runs before exit. |
| `ProgramEntryBuilder.Build` | `ProgramEntry Build()` | Produce the configured `ProgramEntry`. |
| `ProgramEntryOptions.Args` | `string[]? Args { get; init; }` | The command-line arguments passed to `CreateBuilder`. |
| `ControlledExitException.ExitCode` | `int ExitCode { get; set; }` (default `0`) | The exit code returned when this exception is thrown. |

## Exit codes

`RunAsync` maps these outcomes onto the returned exit code:

| Outcome | Returned code | Logged as |
| --- | --- | --- |
| Delegate returns normally | the value your delegate returns | `Debug` — "Program completed with exit code …" |
| Delegate throws `ControlledExitException` | `ex.ExitCode` | `Debug` — "Program exited controlled …" |
| Delegate throws `HostAbortedException` | *(rethrown, no code)* | not logged — see below |
| Delegate throws any other exception | `1` | `Critical` — "Program terminated unexpectedly" (with the exception) |

`HostAbortedException` is deliberately **not** swallowed: design-time tooling (the EF Core tools) and test
hosts (`WebApplicationFactory`) abort the host on purpose and require the exception to propagate out of `Main`
to take over the captured host. Treating it as a crash would break `dotnet ef` and integration tests against
hosts built on `ProgramEntry`.

Because you return the exit code from the delegate, wire it straight into the process exit code by returning the result of `RunAsync` from your top-level statements (as in the examples on this page).

## Controlled exit

Throw a `ControlledExitException` to end the program with a specific, non-error exit code without it being logged as a crash. This is the clean way to stop early — for example after a one-shot task such as running migrations and exiting.

```csharp
return await ProgramEntry.CreateBuilder(args).Build().RunAsync(async options =>
{
    var builder = Host.CreateApplicationBuilder(options.Args);
    var app = builder.Build();

    if (RanOneShotJobAndShouldStop(app))
    {
        // Ends the program cleanly with exit code 0; logged at Debug, not Critical.
        throw new ControlledExitException("Migration job complete") { ExitCode = 0 };
    }

    await app.RunAsync();
    return 0;
});
```

> [!IMPORTANT]
> `ControlledExitException` is only honored when thrown from **inside** the `RunAsync` delegate (or code it awaits). Its default `ExitCode` is `0`; set the property to return a different code. Any other exception type is treated as an unexpected failure and returns `1`.

## Exit actions

Exit actions are cleanup delegates that run in a `finally` block after your startup logic completes — no matter how it ends. Register them on the builder with `AddExitAction`; they run in the order added and each receives the `ProgramEntryOptions`.

```csharp
return await ProgramEntry.CreateBuilder(args)
    .AddExitAction(async options =>
    {
        // Always runs on the way out — flush telemetry, dispose resources, etc.
        await FlushMetricsAsync();
    })
    .Build()
    .RunAsync(async options =>
    {
        var builder = Host.CreateApplicationBuilder(options.Args);
        var app = builder.Build();
        await app.RunAsync();
        return 0;
    });
```

This is exactly how the Serilog addition guarantees the log buffer is flushed: `UseSerilog()` registers an exit action that calls `Log.CloseAndFlushAsync()`.

> [!NOTE]
> Exit actions run even when the program crashes with an unhandled exception. Each exit action is invoked inside its own try/catch: if one throws, the exception is logged at `Error` ("Exit action failed") and the remaining actions still run — a failing exit action never changes the exit code returned by `RunAsync`.

## Replace the logger

The default logger writes to the console. Swap it with `WithLogger` before `Build()` when you want a different sink or the host's own logging pipeline:

```csharp
using Microsoft.Extensions.Logging;

var bootstrapLogger = LoggerFactory
    .Create(logging => logging.AddConsole())
    .CreateLogger("Program");

return await ProgramEntry.CreateBuilder(args)
    .WithLogger(bootstrapLogger)
    .Build()
    .RunAsync(async options =>
    {
        // …
        return 0;
    });
```

For Serilog, prefer the drop-in extension rather than wiring the logger by hand:

```csharp
using ES.FX.Additions.Serilog.Lifetime;

return await ProgramEntry.CreateBuilder(args)
    .UseSerilog()
    .Build()
    .RunAsync(async options =>
    {
        // …
        return 0;
    });
```

`UseSerilog()` sets a Serilog bootstrap logger on the entry and registers the flush exit action for you — see the [Serilog additions](../additions/serilog.md) page.

## See also

- [Serilog additions](../additions/serilog.md) — `ProgramEntryBuilder.UseSerilog()` and console logging setup.
- [Conventions & build config](./conventions.md) — Central Package Management and the shared build settings.
- [Ignite](../ignite/index.md) — the opinionated bootstrap you activate inside the `RunAsync` delegate.
- [Creating a new ES.FX library](./creating-a-library.md) — how packages like `ES.FX.Hosting` are structured.
