using Asp.Versioning;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;

namespace ES.FX.Additions.Asp.Versioning.Builder;

[PublicAPI]
public static class EndpointConventionBuilderExtensions
{
    /// <summary>
    ///     Indicates that the specified API version is supported by the configured endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The extended type.</typeparam>
    /// <param name="builder">The extended endpoint convention builder.</param>
    /// <param name="apiVersions">The supported <see cref="ApiVersion">API versions</see> implemented by the endpoint.</param>
    /// <returns>The original <paramref name="builder" />.</returns>
    public static TBuilder HasApiVersions<TBuilder>(this TBuilder builder, IEnumerable<ApiVersion> apiVersions)
        where TBuilder : IEndpointConventionBuilder
    {
        foreach (var apiVersion in apiVersions) builder.HasApiVersion(apiVersion);
        return builder;
    }
}