namespace ES.FX.Zendesk.MCP.Host.Tests.Hosting;

/// <summary>
///     xUnit collection that serializes the host-boot tests which mutate <em>process-wide</em> environment
///     variables to reach Program.cs's registration-time configuration (the execution baseline and the area
///     gate are read by <c>WebApplication.CreateBuilder</c> before <see cref="Microsoft.AspNetCore.Mvc.Testing" />
///     can inject configuration, so the environment-variable provider is the only seam). Tests within a class run
///     sequentially, but classes run in parallel by default — without this shared collection, two classes toggling
///     the same variable would clobber each other. Membership disables inter-class parallelism for these classes
///     only, leaving the rest of the suite parallel.
/// </summary>
[CollectionDefinition(Name)]
public sealed class HostEnvironmentCollection
{
    /// <summary>The collection name applied via <see cref="CollectionAttribute" /> to member classes.</summary>
    public const string Name = "Host environment variables";
}
