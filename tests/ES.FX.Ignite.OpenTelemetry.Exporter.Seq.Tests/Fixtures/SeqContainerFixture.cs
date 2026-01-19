using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.Fixtures;

public sealed class SeqContainerFixture : IAsyncLifetime
{
    public const string Registry = "datalust";
    public const string Image = "seq";
    public const string Tag = "latest";
    public SeqSutFactory? WebApplicationFactory;
    public IContainer? Container { get; private set; }

    public async Task DisposeAsync()
    {
        if (Container is not null) await Container.DisposeAsync();
        if (WebApplicationFactory is not null) await WebApplicationFactory.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        Container = new ContainerBuilder($"{Registry}/{Image}:{Tag}")
            .WithName($"{nameof(SeqContainerFixture)}-{Guid.CreateVersion7()}")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                .ForPath("/")))
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("SEQ_FIRSTRUN_NOAUTHENTICATION", "true")
            .WithPortBinding(80, true)
            .Build();
        await Container.StartAsync();

        WebApplicationFactory = new SeqSutFactory(GetConnectionString());
    }

    public string GetConnectionString() => $"http://{Container?.Hostname}:{Container?.GetMappedPublicPort(80)}";
}