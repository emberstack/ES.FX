using Asp.Versioning;
using ES.FX.Extensions.Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Moq;

namespace ES.FX.Extensions.Asp.Versioning.Tests;

public class EndpointConventionBuilderExtensionsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    [InlineData(0)]
    public void HasApiVersion_RangeApplied(int numberOfVersions)
    {
        Mock<IEndpointConventionBuilder> mockIEndpointConventionBuilder = new();

        var versions = Enumerable.Range(1, numberOfVersions).ToList().Select(p => new ApiVersion(p));

        mockIEndpointConventionBuilder.Object.HasApiVersions(versions);

        mockIEndpointConventionBuilder.Verify(x => x.Add(It.IsAny<Action<EndpointBuilder>>()),
            Times.Exactly(numberOfVersions));
    }
}