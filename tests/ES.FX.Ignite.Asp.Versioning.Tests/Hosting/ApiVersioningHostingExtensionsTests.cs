using Microsoft.Extensions.Hosting;
using ES.FX.Ignite.Asp.Versioning.Hosting;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

namespace ES.FX.Ignite.Asp.Versioning.Tests.Hosting
{
    public class ApiVersioningHostingExtensionsTests
    {
        [Fact]
        public void IgniteApiVersioningShouldAddTheServices()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);
            Assert.DoesNotContain(builder.Services, s => s.ServiceType == typeof(ApiVersion));
            Assert.DoesNotContain(builder.Services, s => s.ServiceType == typeof(IApiVersionDescriptionProviderFactory));

            builder.IgniteApiVersioning();

            var serviceProvider = builder.Build().Services;
            Assert.Contains(builder.Services, s => s.ServiceType == typeof(ApiVersion));
            Assert.Contains(builder.Services, s => s.ServiceType == typeof(IApiVersionDescriptionProviderFactory));

        }

        [Fact]
        public void IgniteApiVersioningCanBeCalledMultipleTimes()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            builder.IgniteApiVersioning();
            builder.IgniteApiVersioning();
        }
    }
}
