using ES.FX.Ignite.Spark.Configuration;
using Microsoft.Extensions.Configuration;

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

    [Fact]
    public void GetSettings_ReturnsSettings()
    {
        var myConfiguration = new Dictionary<string, string?>
        {
            { "Key1", "Value1" }, { "Nested:Settings:Key1", "NestedValue1" }, { "Nested:Settings:Key2", "NestedValue2" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration.ToList())
            .Build();

        var nestedSettings = SparkConfig.GetSettings<NestedSettings>(configuration, "Nested");
        Assert.False(string.IsNullOrEmpty(nestedSettings.Key1));
        Assert.False(string.IsNullOrEmpty(nestedSettings.Key2));
    }

    internal class NestedSettings
    {
        public string Key1 { get; set; } = string.Empty;
        public string Key2 { get; set; } = string.Empty;
    }
}