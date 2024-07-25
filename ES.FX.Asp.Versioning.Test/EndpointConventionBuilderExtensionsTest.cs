using ES.FX.Asp.Versioning.Builder;
using Asp.Versioning;
using Asp.Versioning.Conventions;
using Microsoft.AspNetCore.Builder;
using Moq;

namespace ES.FX.Asp.Versioning.Test
{
    public class EndpointConventionBuilderExtensionsTest
    {
        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(100)]
        [InlineData(0)]
        public void HasApiVersionCalled(int numberOfVersions)
        {
            Mock<IEndpointConventionBuilder> mockIEndpointConventionBuilder = new();

            var versions = Enumerable.Range(1, numberOfVersions).ToList().Select((p) => new ApiVersion(p));

            mockIEndpointConventionBuilder.Object.HasApiVersions(versions);

            mockIEndpointConventionBuilder.Verify(x => x.Add(It.IsAny<Action<EndpointBuilder>>()), Times.Exactly(numberOfVersions));
        }
    }
}