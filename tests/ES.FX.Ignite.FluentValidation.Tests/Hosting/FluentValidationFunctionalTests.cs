using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using ES.FX.Shared.Tests.Utils;

namespace ES.FX.Ignite.FluentValidation.Tests.Hosting
{
    public class FluentValidationFunctionalTests
    {
        [Theory]
        [InlineData("", true)]
        [InlineData("test", false)]
        public async Task FluentValidation_CheckAutoValidations(string name, bool shouldThrowValidationError)
        {
            var client = WebApplicationFactoryUtils<FluentValidationTestHost>.GetClient();

            var person = new TestRequest(name);

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(person);
            var data = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(FluentValidationTestHost.APIRoute, data);
            string resultContent = await response.Content.ReadAsStringAsync();

            Assert.True(resultContent.Contains("validation error") == shouldThrowValidationError);
        }
    }
}
