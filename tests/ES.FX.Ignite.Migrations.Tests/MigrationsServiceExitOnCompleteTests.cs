using System.Diagnostics;
using System.Reflection;
using ES.FX.Ignite.Migrations.Configuration;
using ES.FX.Ignite.Migrations.Service;
using ES.FX.Migrations.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ES.FX.Ignite.Migrations.Tests;

/// <summary>
///     Covers the <see cref="MigrationsServiceSparkSettings.ExitOnComplete" /> branch of
///     <see cref="MigrationsService.StartAsync" />, which calls <see cref="Environment.Exit" />.
///     Because <c>Environment.Exit(0)</c> terminates the whole process, the branch cannot be
///     exercised inline in the test runner. The parent test re-launches the compiled test
///     assembly (via <c>dotnet exec</c>) filtered to the child test, with an environment flag
///     set so the child actually runs the service and lets the process exit. The parent then
///     asserts the child terminated with exit code 0 — proving both that the branch was taken
///     and that all tasks completed before the exit.
/// </summary>
public class MigrationsServiceExitOnCompleteTests
{
    private const string ChildFlag = "ES_FX_MIGRATIONS_EXIT_CHILD";
    private const string SentinelEnv = "ES_FX_MIGRATIONS_SENTINEL_FILE";

    [Fact]
    public void StartAsync_ExitOnComplete_RunsTasksThenExitsWithCodeZero()
    {
        // Guard: if we somehow entered the child body from the parent context, skip.
        if (Environment.GetEnvironmentVariable(ChildFlag) == "1") return;

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        Assert.False(string.IsNullOrEmpty(assemblyPath), "Test assembly path could not be resolved.");

        // Sentinel file lets us confirm the migration task actually ran inside the child
        // before Environment.Exit(0) fired.
        var sentinel = Path.Combine(Path.GetTempPath(), $"esfx-migrations-exit-{Guid.NewGuid():N}.txt");

        try
        {
            var childMethod =
                $"{typeof(MigrationsServiceExitOnCompleteTests).FullName}.{nameof(ChildBody_RunServiceWithExitOnComplete)}";

            var psi = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("exec");
            psi.ArgumentList.Add(assemblyPath);
            psi.ArgumentList.Add("-method");
            psi.ArgumentList.Add(childMethod);
            psi.Environment[ChildFlag] = "1";
            psi.Environment[SentinelEnv] = sentinel;

            using var process = Process.Start(psi);
            Assert.NotNull(process);

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            var exited = process.WaitForExit(120_000);
            Assert.True(exited, $"Child process did not exit in time.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

            Assert.True(File.Exists(sentinel),
                $"Migration task did not run before exit.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

            // Environment.Exit(0) => the process must terminate with exit code 0.
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (File.Exists(sentinel)) File.Delete(sentinel);
        }
    }

    /// <summary>
    ///     Runs ONLY inside the re-launched child process. Executes the migrations service with
    ///     <see cref="MigrationsServiceSparkSettings.ExitOnComplete" /> = true so that a successful
    ///     run terminates the process via <c>Environment.Exit(0)</c>.
    /// </summary>
    [Fact]
    public async Task ChildBody_RunServiceWithExitOnComplete()
    {
        // Only execute the exit-triggering logic when spawned as the child. When the normal
        // test suite runs this method it must be a harmless no-op (it would otherwise kill the runner).
        if (Environment.GetEnvironmentVariable(ChildFlag) != "1") return;

        var sentinel = Environment.GetEnvironmentVariable(SentinelEnv);

        var task = new Mock<IMigrationsTask>();
        task.Setup(t => t.ApplyMigrations(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (!string.IsNullOrEmpty(sentinel)) File.WriteAllText(sentinel, "ran");
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton(task.Object);
        await using var provider = services.BuildServiceProvider();

        var service = new MigrationsService(
            NullLogger<MigrationsService>.Instance,
            new MigrationsServiceSparkSettings { Enabled = true, ExitOnComplete = true },
            provider);

        await service.StartAsync(CancellationToken.None);

        // If we reach here, Environment.Exit(0) did NOT fire — that's a real failure of the branch.
        throw new InvalidOperationException("Environment.Exit(0) was not called by the ExitOnComplete branch.");
    }
}