---
title: Testing
description: How ES.FX tests are structured with xUnit v3, Moq, Testcontainers, and WebApplicationFactory, and how to run and filter them.
---

Every ES.FX package ships with tests under `tests/`. They fall into three kinds — fast in-memory unit
tests, functional tests that spin up real backing services with Testcontainers, and end-to-end tests
that boot a real ASP.NET host through `WebApplicationFactory`. This page shows how those tests are laid
out, the conventions each one follows, and how to run and filter them locally.

## Test stack

ES.FX standardizes on one test stack across every project. Versions are pinned centrally in
`Directory.Packages.props`, so test `.csproj` files reference packages without a `Version` attribute.

| Package | Role |
| --- | --- |
| `xunit.v3` | Test framework (xUnit **v3**, not v2). |
| `xunit.runner.visualstudio` | IDE / `dotnet test` runner integration. |
| `Microsoft.NET.Test.Sdk` | Test host and discovery. |
| `Moq` | Mocking for isolated unit tests. |
| `coverlet.collector` | Code coverage collection. |
| `Testcontainers.*` | Ephemeral Docker containers for functional tests (`Redis`, `MsSql`, `PostgreSql`, `MariaDb`). |
| `Microsoft.AspNetCore.Mvc.Testing` | In-memory host + `HttpClient` for end-to-end Spark tests. |

> [!IMPORTANT]
> Functional tests that use Testcontainers require a **running Docker engine**. Without Docker, the
> container fixtures fail to start and their tests error out. Pure unit tests (e.g. `ES.FX.Tests`) have
> no such dependency.

## Project layout

Test projects mirror the package they cover and live in `tests/`. Three naming conventions carry meaning:

| Suffix | Purpose |
| --- | --- |
| `{Project}.Tests` | The test project for `{Project}` (unit + functional tests). |
| `{Project}.Tests.SUT` | A real host (ASP.NET app) exercised end-to-end by `{Project}.Tests`. |
| `ES.FX.Shared.{Db}.Tests` | Shared Testcontainers fixtures reused across many test projects. |

A test `.csproj` sets `IsTestProject` and `IsPackable=false`, references the package under test, and
adds the shared fixtures project when it needs a live service:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Moq" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\ES.FX.Ignite.StackExchange.Redis\ES.FX.Ignite.StackExchange.Redis.csproj" />
    <ProjectReference Include="..\ES.FX.Shared.Redis.Tests\ES.FX.Shared.Redis.Tests.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

> [!NOTE]
> `Directory.Build.props` treats any project whose name contains `.Tests` as a test project: it disables
> packing and coverage packaging, and emits a per-project TRX logger. You don't repeat that wiring in the
> `.csproj`. See [Conventions & build config](./conventions.md) for the full set of global properties.

The `<Using Include="Xunit" />` global using is the reason test files reference `[Fact]`, `[Theory]`, and
`Assert` without a `using Xunit;` line.

## Unit tests

Unit tests exercise a type in isolation with no external services. They live directly under `tests/`
mirroring the source folder (for example `tests/ES.FX.Tests/Primitives/OptionalTests.cs` covers
`src/ES.FX/Primitives`). Use `[Fact]` for a single case and `[Theory]` with `[InlineData]` for
parameterized cases.

```csharp
using ES.FX.Primitives;

namespace ES.FX.Tests.Primitives;

public class OptionalTests
{
    [Fact]
    public void Optional_Reference_Can_BeNone()
    {
        var a = Optional<string>.None();
        Assert.False(a.HasValue);
    }

    [Fact]
    public void Optional_Value_Can_HaveValue()
    {
        var a = Optional<int>.From(10);
        Assert.True(a.HasValue);
    }
}
```

For collaborators you want to stub out, use Moq (`new Mock<T>()`). Reach for a real in-memory host or a
Testcontainer instead when the behavior under test is the DI wiring itself.

## Ignite / Spark hosting tests

Spark tests verify configuration binding, DI registration, and the reconfiguration guard **without**
touching a real backing service. They build an empty host with `Host.CreateEmptyApplicationBuilder(null)`,
push configuration in with `AddInMemoryCollection`, call the Spark's `Ignite{Service}...` extension, and
assert on the resolved services.

```csharp
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using ES.FX.Ignite.StackExchange.Redis.Configuration;
using ES.FX.Ignite.StackExchange.Redis.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public class HostingTests
{
    [Fact]
    public void CanOverride_Options()
    {
        const string name = "database";
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{RedisSpark.ConfigurationSectionPath}:{name}:{nameof(RedisSparkOptions.ConnectionString)}",
                "InitialConnectionString")
        ]);

        builder.IgniteRedisClient(name, configureOptions: options =>
            options.ConnectionString = "ChangedConnectionString");

        var app = builder.Build();

        var resolved = app.Services.GetRequiredService<IOptions<RedisSparkOptions>>();
        Assert.Equal("ChangedConnectionString", resolved.Value.ConnectionString);
    }

    [Fact]
    public void IgniteDoesNotAllowReconfiguration()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteRedisClient();

        Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteRedisClient());
    }
}
```

> [!TIP]
> Build config keys from the real constants rather than hand-typing strings: `RedisSpark.ConfigurationSectionPath`
> for the section root, `SparkConfig.Settings` for the `:Settings` sub-node, and `nameof(...)` for property
> names. This keeps tests in lock-step with the Spark's binding. Remember the split — **Options** bind at
> `Ignite:{Service}` directly, while **Settings** bind under the `:Settings` sub-node. See
> [Ignite configuration](../ignite/configuration.md).

Because a duplicate registration for the same key throws, a test can assert the guard fires by calling the
same `Ignite{Service}...` extension twice and expecting `ReconfigurationNotSupportedException`.

## Functional tests with Testcontainers

Functional tests run against a real service in a throwaway Docker container. The container lifetime is
owned by an `IAsyncLifetime` fixture that lives in the shared `ES.FX.Shared.{Db}.Tests` project so many
test projects can reuse it.

```csharp
using Testcontainers.Redis;

namespace ES.FX.Shared.Redis.Tests.Fixtures;

public sealed class RedisContainerFixture : IAsyncLifetime
{
    public const string Image = "redis";
    public const string Tag = "latest";
    public RedisContainer? Container { get; private set; }

    public async ValueTask InitializeAsync()
    {
        Container = new RedisBuilder($"{Image}:{Tag}")
            .WithName($"{nameof(RedisContainerFixture)}-{Guid.CreateVersion7()}")
            .Build();
        await Container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Container is not null) await Container.DisposeAsync();
    }

    public string GetConnectionString() =>
        Container?.GetConnectionString() ??
        throw new InvalidOperationException("The test container was not initialized.");
}
```

A test class consumes the fixture with `IClassFixture<T>` (one container shared across the class's tests)
and feeds its connection string into the Spark's `configureOptions` delegate:

```csharp
public class FunctionalTests(RedisContainerFixture redisFixture)
    : IClassFixture<RedisContainerFixture>
{
    [Theory]
    [InlineData("my-key", "my-value")]
    public async Task CanConnect(string key, string value)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.IgniteRedisClient("database", configureOptions: options =>
            options.ConnectionString = redisFixture.GetConnectionString());

        var app = builder.Build();

        var connection = app.Services.GetRequiredService<IConnectionMultiplexer>();
        var database = connection.GetDatabase();
        await database.StringSetAsync(key, value);

        Assert.Equal(value, await database.StringGetAsync(key));
    }
}
```

> [!NOTE]
> Async tests receive `TestContext.Current.CancellationToken` from xUnit v3 — pass it to awaited calls so
> the test respects the runner's timeout and cancellation. Shared fixtures for SQL Server, PostgreSQL, and
> MariaDB follow the same `IAsyncLifetime` shape (`SqlServerContainerFixture`, `PostgreSqlContainerFixture`,
> `MySqlContainerFixture`), each pinning a specific image and tag.

## End-to-end tests with a system-under-test (SUT)

To test middleware and endpoints that only exist on a real web host, ES.FX uses a companion
`{Project}.Tests.SUT` project — a minimal ASP.NET app that activates the Spark exactly as a consumer
would. The test project references both the Spark and its SUT and boots the SUT with
`WebApplicationFactory<TEntryPoint>` from `Microsoft.AspNetCore.Mvc.Testing`.

The SUT is an ordinary host whose `Program` class is made public for the factory to key off:

```csharp
using ES.FX.Ignite.NSwag.Hosting;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();
app.IgniteNSwag();

app.Run();

namespace ES.FX.Ignite.NSwag.Tests.SUT
{
    public class Program;
}
```

The test resolves an `HttpClient` from the factory and asserts against real HTTP responses:

```csharp
using ES.FX.Ignite.NSwag.Tests.SUT;
using Microsoft.AspNetCore.Mvc.Testing;

public class NSwagFunctionalTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Swagger_Accessible()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/swagger/", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
    }
}
```

> [!TIP]
> Reach for the SUT pattern when the thing you're verifying is a request-pipeline behavior — a mapped
> endpoint, response header middleware, health-check routes, or generated OpenAPI. For pure DI/config
> assertions, the lighter `Host.CreateEmptyApplicationBuilder(null)` approach is enough.

## Run the tests

`dotnet test` discovers and runs every test project in the solution. Results are written as TRX files to
`.artifacts/TestResults/`.

```bash
# Run the whole suite (Docker must be running for functional tests)
dotnet test --verbosity normal

# Run a single test project
dotnet test tests/ES.FX.Tests/ES.FX.Tests.csproj

# Detailed console output
dotnet test --logger "console;verbosity=detailed"
```

Filter to a subset with `--filter` and xUnit's `FullyQualifiedName` selector:

```bash
# One class
dotnet test --filter "FullyQualifiedName~HostingTests"

# One test method
dotnet test --filter "FullyQualifiedName~HostingTests.IgniteDoesNotAllowReconfiguration"
```

> [!TIP]
> To skip the Docker-dependent functional tests on a machine without a Docker engine, run only the
> pure-unit project (`dotnet test tests/ES.FX.Tests/ES.FX.Tests.csproj`) or narrow with `--filter` to the
> hosting/unit classes you care about.

## See also

- [Development](./index.md) — building, packing, and running the playground.
- [Conventions & build config](./conventions.md) — the global `Directory.Build.props` rules that configure test projects.
- [Creating a new ES.FX library](./creating-a-library.md) — scaffolding a package and its test project.
- [Ignite configuration](../ignite/configuration.md) — the `Ignite:` root and the Settings-vs-Options split that hosting tests assert against.
- [Testcontainers for .NET](https://dotnet.testcontainers.org/) — the container library behind the shared fixtures.
