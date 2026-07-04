using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests;

public class DbContextSparkTests
{
    [Fact]
    public void Name_IsDbContext()
    {
        Assert.Equal("DbContext", DbContextSpark.Name);
    }

    [Fact]
    public void ConfigurationSectionPath_IsIgniteColonDbContext()
    {
        Assert.Equal($"{IgniteConfigurationSections.Ignite}:{DbContextSpark.Name}",
            DbContextSpark.ConfigurationSectionPath);
    }

    [Fact]
    public void ConfigurationSectionPath_ResolvesToExpectedLiteral()
    {
        // Locks the composed literal so a rename of either constant is caught.
        Assert.Equal("Ignite:DbContext", DbContextSpark.ConfigurationSectionPath);
    }
}