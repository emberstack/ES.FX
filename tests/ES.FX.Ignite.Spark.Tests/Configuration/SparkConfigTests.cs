using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Spark.Tests.Configuration;

public class SparkConfigTests
{
    [Fact]
    public void Name_UsesDefaultIfNull()
    {
        const string defaultName = "default";
        var name = SparkConfig.Name(null, defaultName);

        Assert.Equal(defaultName, name);
    }

    [Fact]
    public void Name_UsesNameIfNotNull()
    {
        const string defaultName = "default";
        const string serviceName = "key";
        var name = SparkConfig.Name(serviceName, defaultName);

        Assert.Equal(serviceName, name);
    }


    [Fact]
    public void Path_UsesNameIfSectionIsNull()
    {
        const string name = "name";
        var configPath = SparkConfig.Path(name, string.Empty);

        Assert.Equal(name, configPath);
    }

    [Fact]
    public void Path_UsesNameAndSection()
    {
        const string section = "section";
        const string name = "name";
        var configPath = SparkConfig.Path(name, section);

        Assert.Equal($"{section}:{name}", configPath);
    }
}