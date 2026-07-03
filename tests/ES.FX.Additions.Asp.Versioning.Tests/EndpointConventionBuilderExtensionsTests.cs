using Asp.Versioning;
using Asp.Versioning.Builder;
using ES.FX.Additions.Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Additions.Asp.Versioning.Tests;

public class EndpointConventionBuilderExtensionsTests
{
    // ----------------------------------------------------------------------
    // Structural contract of HasApiVersions using a lightweight fake builder.
    // These assert the extension's own logic (guard, fluency, per-element
    // forwarding) without needing the full routing/versioning collation pass.
    // ----------------------------------------------------------------------

    /// <summary>A minimal <see cref="IEndpointConventionBuilder" /> that captures the conventions added to it.</summary>
    private sealed class CapturingConventionBuilder : IEndpointConventionBuilder
    {
        public List<Action<EndpointBuilder>> Conventions { get; } = [];

        public void Add(Action<EndpointBuilder> convention) => Conventions.Add(convention);

        /// <summary>Replays the captured conventions against a fresh endpoint builder and returns its metadata.</summary>
        public IList<object> ReplayMetadata()
        {
            var endpointBuilder = new RouteEndpointBuilder(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse("/test"),
                0);
            foreach (var convention in Conventions) convention(endpointBuilder);
            return endpointBuilder.Metadata;
        }
    }

    [Fact]
    public void HasApiVersions_NullApiVersions_ThrowsArgumentNullException()
    {
        var builder = new CapturingConventionBuilder();

        var ex = Assert.Throws<ArgumentNullException>(() => builder.HasApiVersions(null!));
        Assert.Equal("apiVersions", ex.ParamName);
    }

    [Fact]
    public void HasApiVersions_ReturnsSameBuilderInstance_ForFluentChaining()
    {
        var builder = new CapturingConventionBuilder();

        var returned = builder.HasApiVersions([new ApiVersion(1, 0)]);

        Assert.Same(builder, returned);
    }

    [Fact]
    public void HasApiVersions_AddsOneConventionPerVersion()
    {
        var builder = new CapturingConventionBuilder();

        builder.HasApiVersions([new ApiVersion(1, 0), new ApiVersion(2, 0), new ApiVersion(3, 0)]);

        // The implementation forwards each element to HasApiVersion(...) individually.
        Assert.Equal(3, builder.Conventions.Count);
    }

    [Fact]
    public void HasApiVersions_EachConvention_ReadsTheEndpointsVersionSet()
    {
        var builder = new CapturingConventionBuilder();
        builder.HasApiVersions([new ApiVersion(1, 0), new ApiVersion(2, 0)]);

        // Each convention produced by HasApiVersion(...) requires (and reads) the endpoint's
        // ApiVersionSet at convention time. Replaying without one is expected to throw; replaying
        // with one seeded (as WithApiVersionSet does) must succeed for every convention.
        var endpointBuilder = new RouteEndpointBuilder(
            _ => Task.CompletedTask, RoutePatternFactory.Parse("/test"), 0);
        endpointBuilder.Metadata.Add(new ApiVersionSetBuilder("test").Build());

        var exception = Record.Exception(() =>
        {
            foreach (var convention in builder.Conventions) convention(endpointBuilder);
        });

        Assert.Null(exception);
        Assert.Equal(2, builder.Conventions.Count);
    }

    [Fact]
    public void HasApiVersions_ConventionsRequireAnAssociatedVersionSet()
    {
        var builder = new CapturingConventionBuilder();
        builder.HasApiVersions([new ApiVersion(1, 0)]);

        // Documents the real Asp.Versioning contract the helper defers to: a version set must exist.
        Assert.Throws<InvalidOperationException>(() => builder.ReplayMetadata());
    }

    [Fact]
    public void HasApiVersions_EmptyEnumerable_AddsNoConventions()
    {
        var builder = new CapturingConventionBuilder();

        var returned = builder.HasApiVersions([]);

        Assert.Same(builder, returned);
        Assert.Empty(builder.Conventions);
        Assert.DoesNotContain(builder.ReplayMetadata(), m => m is IApiVersionProvider);
    }

    [Fact]
    public void HasApiVersions_LazilyEnumeratesApiVersionsExactlyOnce()
    {
        var builder = new CapturingConventionBuilder();
        var produced = 0;

        IEnumerable<ApiVersion> Versions()
        {
            produced++;
            yield return new ApiVersion(1, 0);
            produced++;
            yield return new ApiVersion(2, 0);
        }

        builder.HasApiVersions(Versions());

        // Enumeration completes exactly once over the source sequence, one convention per element.
        Assert.Equal(2, produced);
        Assert.Equal(2, builder.Conventions.Count);
    }

    // ----------------------------------------------------------------------
    // End-to-end behavior through a real minimal-API host. This exercises the
    // full ASP.NET routing + Asp.Versioning collation and asserts the actual
    // ApiVersionMetadata the endpoint ends up carrying.
    // ----------------------------------------------------------------------

    private static async Task<ApiVersionModel> DeclareVersionsEndToEndAsync(
        Action<IEndpointConventionBuilder> declare,
        CancellationToken cancellationToken)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.Services.AddApiVersioning();
        appBuilder.Services.AddRouting();
        await using var app = appBuilder.Build();

        var versionSet = app.NewApiVersionSet("e2e").Build();
        var endpoint = app.MapGet("/e2e", () => Results.Ok()).WithApiVersionSet(versionSet);
        declare(endpoint);

        await app.StartAsync(cancellationToken);
        try
        {
            var dataSource = app.Services.GetRequiredService<EndpointDataSource>();
            var metadata = dataSource.Endpoints
                .Select(e => e.Metadata.GetMetadata<ApiVersionMetadata>())
                .Where(m => m is not null)
                .Select(m => m!.Map(ApiVersionMapping.Explicit))
                .Single(m => m.DeclaredApiVersions.Count > 0);
            return metadata;
        }
        finally
        {
            await app.StopAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task HasApiVersions_EndToEnd_DeclaresAndSupportsAllSuppliedVersions()
    {
        var expected = new[] { new ApiVersion(1, 0), new ApiVersion(2, 0), new ApiVersion(2, 1) };

        var model = await DeclareVersionsEndToEndAsync(
            e => e.HasApiVersions(expected),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, model.DeclaredApiVersions);
        Assert.Equal(expected, model.SupportedApiVersions);
    }

    [Fact]
    public async Task HasApiVersions_EndToEnd_SingleVersion_IsDeclared()
    {
        var model = await DeclareVersionsEndToEndAsync(
            e => e.HasApiVersions([new ApiVersion(3, 0)]),
            TestContext.Current.CancellationToken);

        Assert.Equal([new ApiVersion(3, 0)], model.DeclaredApiVersions);
    }

    [Fact]
    public async Task HasApiVersions_EndToEnd_MergesWithVersionsDeclaredViaHasApiVersion()
    {
        // A version added the "native" single-version way must coexist with the batch helper's versions.
        var model = await DeclareVersionsEndToEndAsync(
            e =>
            {
                e.HasApiVersion(new ApiVersion(1, 0));
                e.HasApiVersions([new ApiVersion(2, 0), new ApiVersion(3, 0)]);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [new ApiVersion(1, 0), new ApiVersion(2, 0), new ApiVersion(3, 0)],
            model.DeclaredApiVersions);
    }

    [Fact]
    public async Task HasApiVersions_EndToEnd_DuplicateVersions_AreDeduplicated()
    {
        var model = await DeclareVersionsEndToEndAsync(
            e => e.HasApiVersions([new ApiVersion(1, 0), new ApiVersion(1, 0), new ApiVersion(2, 0)]),
            TestContext.Current.CancellationToken);

        // The versioning layer collates declarations into a distinct, ordered set.
        Assert.Equal([new ApiVersion(1, 0), new ApiVersion(2, 0)], model.DeclaredApiVersions);
    }
}
