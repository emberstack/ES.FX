using ES.FX.Microsoft.Data.SqlClient.Factories;
using Microsoft.Data.SqlClient;
using Moq;

namespace ES.FX.Microsoft.Data.SqlClient.Tests.Factories;

public class DelegateSqlConnectionFactoryTests
{
    [Fact]
    public void DelegateFactory_CanCreateConnections()
    {
        var connection = new SqlConnection();

        var serviceProviderMock = new Mock<IServiceProvider>();
        var funcMock = new Mock<Func<IServiceProvider, SqlConnection>>();
        funcMock.Setup(mock => mock(serviceProviderMock.Object)).Returns(connection);

        var delegateSqlConnectionFactory =
            new DelegateSqlConnectionFactory(serviceProviderMock.Object, funcMock.Object);

        var returnedConnection = delegateSqlConnectionFactory.CreateConnection();

        funcMock.Verify(mock => mock(serviceProviderMock.Object), Times.Once);
        Assert.Equal(connection, returnedConnection);
    }
}