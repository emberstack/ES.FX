using ES.FX.Microsoft.EntityFrameworkCore.Factories;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Tests.Factories
{
    public class DelegateDbContextFactoryTests
    {
        [Fact]
        public void DelegateFactory_CanCreateDbContext()
        {
            var serviceProviderMock = new Mock<IServiceProvider>();
            var funcMock = new Mock<Func<IServiceProvider, DbContext>>();

            DelegateDbContextFactory<DbContext> delegateSqlConnectionFactory = new DelegateDbContextFactory<DbContext>(serviceProviderMock.Object, funcMock.Object);

            var returnedConnection = delegateSqlConnectionFactory.CreateDbContext();

            funcMock.Verify(funcMock => funcMock(serviceProviderMock.Object), Times.Once);
        }
    }
}
