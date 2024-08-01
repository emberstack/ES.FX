using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;

namespace ES.FX.Ignite.FluentValidation.Tests.Hosting
{
    public class FluentValidationFunctionalTests
    {
        [Theory]
        [InlineData("", true)]
        [InlineData("test", false)]
        public async Task FluentValidation_CheckAutoValidations(string name, bool shouldThrowValidationError)
        {
            var client = GetClient();

            var person = new TestRequest(name);

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(person);
            var data = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(FluentValidationTestHost.APIRoute, data);
            string resultContent = await response.Content.ReadAsStringAsync();

            Assert.True(resultContent.Contains("validation error") == shouldThrowValidationError);
        }

        private HttpClient GetClient()
        {
            var application = new WebApplicationFactory<FluentValidationTestHost>()
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
