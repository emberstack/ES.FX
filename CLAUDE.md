# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ES.FX (EmberStack Framework) is a comprehensive collection of extensions and application frameworks for .NET. It provides reusable components for building enterprise-grade applications with built-in observability, resilience, and clean architecture patterns.

## Commands

### Build and Test
```bash
# Build the solution
dotnet build
dotnet build --configuration Release

# Run all tests
dotnet test --verbosity normal

# Run tests for a specific project
dotnet test tests/ES.FX.Tests/ES.FX.Tests.csproj

# Clean build artifacts
dotnet clean
```

### Package Management
```bash
# Create NuGet packages (automatically done during build for ES.FX.* projects)
dotnet pack

# Restore packages
dotnet restore
```

### Running Locally
```bash
# Run API playground
dotnet run --project playground/Playground.Microservice.Api.Host

# Run worker playground
dotnet run --project playground/Playground.Microservice.Worker.Host

# Run test SUTs for manual testing
dotnet run --project tests/ES.FX.Ignite.AspNetCore.HealthChecks.UI.Tests.SUT
```

### Code Quality
```bash
# Format code
dotnet format

# Run code analysis
dotnet build /p:RunAnalyzers=true
```

## Architecture

### Layer Structure
1. **ES.FX** - Core abstractions and primitives
   - `IMessenger`, `IMessage`, `IMessageHandler` - Messaging abstractions
   - `Result<T>` - Result pattern for error handling
   - `Problem` - Standardized error representation
   - `Optional<T>` - Nullable value handling

2. **ES.FX.Additions.*** - Extensions for third-party libraries
   - MassTransit - Message bus integration
   - MediatR - Mediator pattern with batch processing
   - FluentValidation - Validation with Problem pattern
   - Microsoft.EntityFrameworkCore - EF Core utilities

3. **ES.FX.Hosting** - Application lifecycle management
   - `ProgramEntry` - Structured program entry with error handling
   - Graceful shutdown and logging integration

4. **ES.FX.Ignite** - Opinionated application framework
   - Built-in OpenTelemetry, health checks, resilience
   - Spark components for service integrations (Redis, SQL Server, Azure services, etc.)

5. **ES.FX.TransactionalOutbox** - Outbox pattern implementation
   - Reliable message delivery with EF Core
   - Automatic message capture via interceptors

### Key Patterns
- **Builder Pattern** - Used for configuration (e.g., `ProgramEntryBuilder`)
- **Options Pattern** - All configurations use `IOptions<T>`
- **Extension Methods** - Clean API surface
- **Dependency Injection** - All components designed for DI
- **Modular Architecture** - Each component can be used independently

## Development Guidelines

### Project Configuration
- All projects target .NET 9.0
- Warnings are treated as errors (`TreatWarningsAsErrors=true`)
- Nullable reference types are enabled (`Nullable=enable`)
- Implicit usings are enabled (`ImplicitUsings=enable`)
- XML documentation is generated for all projects

### Testing
- Unit tests use xUnit
- Functional tests use Testcontainers for Redis and SQL Server
- Test projects follow naming convention: `{ProjectName}.Tests`
- SUT (System Under Test) projects for integration testing

### Naming Conventions
- Namespaces: `ES.FX.{Component}.{SubComponent}`
- Spark components: `ES.FX.Ignite.Spark.{ServiceName}`
- Test fixtures: `{Service}Fixture` (e.g., `RedisFixture`, `SeqContainerFixture`)

### Adding New Components
1. Create project in appropriate folder (src/ES.FX.*)
2. Follow existing project structure and naming
3. Add corresponding test project
4. Use central package management (Directory.Packages.props)
5. Implement health checks where applicable
6. Add OpenTelemetry instrumentation if relevant

### Common Tasks

#### Adding a New Spark Component
1. Create project: `src/ES.FX.Ignite.Spark.{ServiceName}`
2. Create configuration class: `{ServiceName}SparkConfig`
3. Create hosting extensions: `{ServiceName}SparkHostingExtensions`
4. Implement health checks if applicable
5. Add tests in `tests/ES.FX.Ignite.Spark.{ServiceName}.Tests`

#### Working with Transactional Outbox
1. Add `ES.FX.TransactionalOutbox.Microsoft.EntityFrameworkCore` reference
2. Configure in DbContext with `UseTransactionalOutbox()`
3. Messages are automatically captured when saving changes
4. Use `OutboxDeliveryService` for message delivery

### Important Files
- `Directory.Build.props` - Global build configuration
- `Directory.Packages.props` - Central package versions
- `ES.FX.slnx` - Solution structure
- `GitVersion.yaml` - Versioning configuration
- `.github/workflows/pipeline.yaml` - CI/CD pipeline

### CI/CD
- Automated builds on push/PR
- Semantic versioning with GitVersion
- Publishes to GitHub Packages and NuGet.org
- Creates GitHub releases automatically
- Path filters optimize build times