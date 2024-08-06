using Microsoft.AspNetCore.Builder;
using ES.FX.Ignite.Serilog.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ES.FX.Ignite.Spark.Exceptions;

namespace ES.FX.Ignite.Serilog.Tests.Hosting
{
    public class SerilogHostingExtensionsTests
    {
        [Fact]
        public void CanIgnite()
        {
            var builder = WebApplication.CreateBuilder([]);

            builder.IgniteSerilog();

            var serviceProvider = builder.Build().Services;
            Assert.NotNull(serviceProvider.GetRequiredService<ILogger>());
        }


        [Fact]
        public void CanIgnite_Once()
        {
            var builder = WebApplication.CreateBuilder([]);

            builder.IgniteSerilog();

            Assert.Throws<ReconfigurationNotSupportedException>(() => builder.IgniteSerilog());
        }
    }
}
