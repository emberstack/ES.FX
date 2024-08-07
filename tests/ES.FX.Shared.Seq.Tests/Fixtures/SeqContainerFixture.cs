using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace ES.FX.Shared.Seq.Tests.Fixtures;

public sealed class SeqContainerFixture : IAsyncLifetime
{
    public const string Registry = "datalust";
    public const string Image = "seq";
    public const string Tag = "latest";
    public IContainer? Container { get; private set; }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder()
            .WithName($"{nameof(SeqContainerFixture)}-{Guid.NewGuid()}")
            .WithImage($"{Registry}/{Image}:{Tag}")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                            .ForPath("/")))
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithPortBinding(5341, 80)
            .Build();
        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (Container is not null) await Container.DisposeAsync();
    }

    public string GetConnectionString() => $"http://{Container?.Hostname}:{Container?.GetMappedPublicPort(80)}";
}