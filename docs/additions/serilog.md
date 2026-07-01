---
title: Serilog additions
description: Wire Serilog into the ProgramEntry lifecycle with a preconfigured bootstrap logger and log enrichers.
---

## Overview

`ES.FX.Additions.Serilog` augments [Serilog](https://serilog.net/) with the glue that connects it to the
ES.FX [application hosting](../development/hosting.md) lifecycle. Its centerpiece is
`ProgramEntryBuilder.UseSerilog()`, which stands up a Serilog **bootstrap logger** before the host is
built, points `ProgramEntry` at it, and flushes it cleanly on exit — so startup and shutdown logs are
never lost. The package also ships a few small enrichers and a default console output template.

This Addition adds helpers **on top of** Serilog; it does not replace or re-explain the base Serilog API.
For sinks, minimum-level overrides, and configuration syntax, see the upstream
[Serilog documentation](https://github.com/serilog/serilog/wiki).

> [!TIP]
> Using Ignite? The [Serilog Spark](../ignite/sparks/serilog.md) wires Serilog into the host as the
> application logger (reading `Serilog:` configuration and enriching from the DI container). Use the Spark
> for the running host; use this Addition's `UseSerilog()` for the `ProgramEntry` bootstrap phase. They
> compose — the playground uses both.

## Install

```bash
dotnet add package ES.FX.Additions.Serilog
```

```xml
<PackageReference Include="ES.FX.Additions.Serilog" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so an in-repo `<PackageReference>` carries no `Version`
> attribute. In a standalone consuming project that does not centralize versions, add `Version="…"`.

## What it adds

| Member | Signature | Purpose |
| --- | --- | --- |
| `ProgramEntryBuilder.UseSerilog` | `UseSerilog(this ProgramEntryBuilder builder, LogEventLevel minimumLevel = LogEventLevel.Information, Action<LoggerConfiguration>? configureLoggerConfiguration = null, bool enableConsoleSelfLog = true)` | Builds a Serilog bootstrap logger, sets it as the `ProgramEntry` logger, and registers an exit action that closes and flushes Serilog. |
| `EntryAssemblyNameEnricher` | `class EntryAssemblyNameEnricher` (an `ILogEventEnricher`) | Adds the `ApplicationEntryAssembly` property (the entry assembly's full name) to each log event. Applied by default in `UseSerilog()`. |
| `CachedPropertyEnricher` | `abstract class CachedPropertyEnricher : ILogEventEnricher` | Base class for enrichers that add a single, constant `LogEventProperty`. Override `CreateProperty(ILogEventPropertyFactory)`; the result is computed once, cached, and added with `AddPropertyIfAbsent`. `EntryAssemblyNameEnricher` is the shipped example — subclass it to build your own. |
| `ApplicationNameEnricher` | `class ApplicationNameEnricher(IHostEnvironment hostEnvironment)` | Adds the `ApplicationName` property from `IHostEnvironment`. Opt-in — add it yourself once an `IHostEnvironment` is available. |
| `ConsoleOutputTemplates.Default` | `const string` | The default console output template used by `UseSerilog()` (`{Timestamp:…} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}`). |

### What `UseSerilog()` configures out of the box

The bootstrap logger `UseSerilog()` creates comes preconfigured with:

- Minimum level set to `minimumLevel` (default `Information`).
- A console sink using `ConsoleOutputTemplates.Default`.
- Destructuring limits: max collection count 64, max string length 2048, max depth 16.
- Enrichment from `LogContext`, machine name, environment name, and `EntryAssemblyNameEnricher`.
- Serilog's `SelfLog` written to `Console.Error` when `enableConsoleSelfLog` is `true` (the default),
  which surfaces Serilog's own internal errors.

The `configureLoggerConfiguration` delegate runs **after** these defaults, so you can add sinks or
override anything before the logger is created.

> [!NOTE]
> The logger created here is a Serilog **bootstrap logger** (`CreateBootstrapLogger()`). It captures
> logs during startup and is replaced by the host's logger once the host is built — for example by the
> [Serilog Spark](../ignite/sparks/serilog.md) or another `Serilog.AspNetCore` integration.

## Usage

### Add Serilog to the ProgramEntry

Chain `UseSerilog()` onto the `ProgramEntryBuilder` before `Build()`. This is the first thing to run, so
that everything after it — including host construction failures — is logged.

```csharp
using ES.FX.Additions.Serilog.Lifetime;
using ES.FX.Hosting.Lifetime;

return await ProgramEntry.CreateBuilder(args)
    .UseSerilog()
    .Build()
    .RunAsync(async _ =>
    {
        var builder = WebApplication.CreateBuilder(args);
        // builder.IgniteSerilog();  // hand off to the host logger (Serilog Spark)
        // builder.Ignite();
        var app = builder.Build();
        // app.Ignite();
        await app.RunAsync();
        return 0;
    });
```

`UseSerilog()` also registers an exit action via `builder.AddExitAction(...)` that calls
`Log.CloseAndFlushAsync()`, so buffered log events are flushed when the program exits.

### Set the minimum level and add sinks

Raise or lower the bootstrap minimum level and add your own sinks through
`configureLoggerConfiguration`. The delegate receives the same `LoggerConfiguration` the defaults were
applied to.

```csharp
using Serilog;
using Serilog.Events;

ProgramEntry.CreateBuilder(args)
    .UseSerilog(
        minimumLevel: LogEventLevel.Debug,
        configureLoggerConfiguration: logger =>
        {
            logger.WriteTo.File("logs/bootstrap-.log", rollingInterval: RollingInterval.Day);
        });
```

### Use the default console template outside UseSerilog

`ConsoleOutputTemplates.Default` is public, so you can reuse the same console format when you configure
Serilog directly (for example on the host logger).

```csharp
using ES.FX.Additions.Serilog.Sinks.Console;
using Serilog;

new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: ConsoleOutputTemplates.Default)
    .CreateLogger();
```

### Add the enrichers

`EntryAssemblyNameEnricher` is applied automatically by `UseSerilog()`. Add it (or
`ApplicationNameEnricher`, which needs an `IHostEnvironment`) explicitly when configuring a logger
yourself.

```csharp
using ES.FX.Additions.Serilog.Enrichers;
using Serilog;

new LoggerConfiguration()
    .Enrich.With<EntryAssemblyNameEnricher>()               // ApplicationEntryAssembly property
    .Enrich.With(new ApplicationNameEnricher(hostEnvironment)) // ApplicationName property
    .CreateLogger();
```

To add your own constant property, subclass `CachedPropertyEnricher` and override `CreateProperty`.
The value is computed once, cached, and added with `AddPropertyIfAbsent` on every event —
`EntryAssemblyNameEnricher` is the shipped example.

```csharp
using ES.FX.Additions.Serilog.Enrichers;
using Serilog.Core;
using Serilog.Events;

public sealed class BuildIdEnricher : CachedPropertyEnricher
{
    protected override LogEventProperty CreateProperty(ILogEventPropertyFactory propertyFactory) =>
        propertyFactory.CreateProperty("BuildId", Environment.GetEnvironmentVariable("BUILD_ID"));
}
```

## Notes and limitations

- **Bootstrap scope.** `UseSerilog()` targets the `ProgramEntry` lifecycle only. It creates a bootstrap
  logger meant to be superseded by the host logger; it does not configure the host's `ILoggerFactory` by
  itself. For that, use the [Serilog Spark](../ignite/sparks/serilog.md).
- **`ApplicationNameEnricher` is opt-in.** It depends on `IHostEnvironment`, which is not available during
  the pre-host bootstrap phase, so `UseSerilog()` does not add it automatically.
- **Single dependency.** Per the Additions charter, this package augments Serilog and only Serilog. It
  does not pull in Ignite or configure OpenTelemetry — those live in the Ignite layer.

## See also

- [Serilog Spark](../ignite/sparks/serilog.md) — wire Serilog as the running host's logger with Ignite.
- [Application hosting](../development/hosting.md) — the `ProgramEntry` / `ProgramEntryBuilder` lifecycle.
- [Additions](./index.md) — the full catalog of single-dependency helper packages.
- [Serilog documentation](https://github.com/serilog/serilog/wiki) — the upstream logging API.
