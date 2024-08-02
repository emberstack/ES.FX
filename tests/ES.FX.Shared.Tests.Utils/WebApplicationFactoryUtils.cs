using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Shared.Tests.Utils
{
    public static class WebApplicationFactoryUtils<T> where T : class
    {
        public static HttpClient GetClient()
        {
            var application = new WebApplicationFactory<T>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddAuthorization();
                        services.AddControllers();
                    });
                });

            var client = application.CreateClient();
            return client;
        }
    }
}
