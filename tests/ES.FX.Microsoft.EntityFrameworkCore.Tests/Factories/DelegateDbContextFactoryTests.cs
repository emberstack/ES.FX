using ES.FX.Microsoft.EntityFrameworkCore.Factories;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ES.FX.Microsoft.EntityFrameworkCore.Tests.Factories;

public class DelegateDbContextFactoryTests
{
    [Fact]
    public void DelegateFactory_CanCreateDbContext()
    {
        var serviceProviderMock = new Mock<IServiceProvider>();
        var funcMock = new Mock<Func<IServiceProvider, DbContext>>();

        var delegateSqlConnectionFactory =
            new DelegateDbContextFactory<DbContext>(serviceProviderMock.Object, funcMock.Object);

        delegateSqlConnectionFactory.CreateDbContext();

        funcMock.Verify(mock => mock(serviceProviderMock.Object), Times.Once);
    }
}