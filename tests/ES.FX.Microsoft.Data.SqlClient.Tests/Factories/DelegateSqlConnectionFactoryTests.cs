using ES.FX.Microsoft.Data.SqlClient.Factories;
using Microsoft.Data.SqlClient;
using Moq;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Tests.Factories
{
    public class DelegateSqlConnectionFactoryTests
    {
        [Fact]
        public void DelegateFactory_CanCreateConnections()
        {
            SqlConnection connection = new SqlConnection();

            var serviceProviderMock = new Mock<IServiceProvider>();
            var funcMock = new Mock<Func<IServiceProvider, SqlConnection>>();
            funcMock.Setup(funcMock => funcMock(serviceProviderMock.Object)).Returns(connection);

            DelegateSqlConnectionFactory delegateSqlConnectionFactory = new DelegateSqlConnectionFactory(serviceProviderMock.Object, funcMock.Object);

            var returnedConnection = delegateSqlConnectionFactory.CreateConnection();

            funcMock.Verify(funcMock => funcMock(serviceProviderMock.Object), Times.Once);
            Assert.Equal(connection, returnedConnection);
        }
    }
}
