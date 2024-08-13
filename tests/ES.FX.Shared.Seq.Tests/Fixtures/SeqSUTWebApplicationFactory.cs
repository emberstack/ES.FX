using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ES.FX.Shared.Seq.Tests.Fixtures
{
    public class SeqSUTWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        public SeqSUTWebApplicationFactory(string seqConnectionString)
        {
            SeqConnectionString = seqConnectionString;
        }

        public string SeqConnectionString { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(Program.ConnectionStringArgument, SeqConnectionString);
        }
    }
}
