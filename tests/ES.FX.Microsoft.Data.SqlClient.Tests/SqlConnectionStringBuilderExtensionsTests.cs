using Microsoft.Data.SqlClient;

namespace ES.FX.Microsoft.Data.SqlClient.Tests;

public class SqlConnectionStringBuilderExtensionsTests
{
    [Fact]
    public void CloneForMaster()
    {
        var connectionString = "Data Source=.;Initial Catalog=initial;Integrated Security=True";
        var builder = new SqlConnectionStringBuilder();
        builder.ConnectionString = connectionString;

        var builderVerification = new SqlConnectionStringBuilder();
        builderVerification.ConnectionString = connectionString;
        builderVerification.SetInitialCatalogToMaster();

        var cloned = builder.CloneForMaster();
        Assert.Equal(cloned.ConnectionString, builderVerification.ConnectionString);
    }

    [Fact]
    public void SetInitialCatalog()
    {
        var catalog = "catalog";
        var builder = new SqlConnectionStringBuilder();
        builder.SetInitialCatalog(catalog);
        Assert.Equal(catalog, builder.InitialCatalog);
    }

    [Fact]
    public void SetInitialCatalogToMaster()
    {
        var builder = new SqlConnectionStringBuilder();
        builder.SetInitialCatalogToMaster();
        Assert.Equal("master", builder.InitialCatalog);
    }
}