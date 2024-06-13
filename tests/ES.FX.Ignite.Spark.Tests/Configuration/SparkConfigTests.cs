using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.Spark.Tests.Configuration
{
    public class SparkConfigTests
    {
        [Fact]
        public void SparkConfig_Key_UsesDefaultIfNull()
        {
            const string defaultKey = "default";
            var key = SparkConfig.Key(null, defaultKey);

            Assert.Equal(defaultKey, key);
        }

        [Fact]
        public void SparkConfig_Key_UsesKeyIfNotNull()
        {
            const string defaultKey = "default";
            const string properKey = "key";
            var key = SparkConfig.Key(properKey, defaultKey);

            Assert.Equal(properKey, key);
        }


        [Fact]
        public void SparkConfig_ConfigurationKey_UsesKeyIfSectionIsNull()
        {
            const string key = "key";
            var configurationKey = SparkConfig.ConfigurationKey(key, string.Empty);

            Assert.Equal(key, configurationKey);
        }

        [Fact]
        public void SparkConfig_ConfigurationKey_UsesKeyAndSection()
        {
            const string section = "section";
            const string key = "key";
            var configurationKey = SparkConfig.ConfigurationKey(key, section);

            Assert.Equal($"{section}:{key}", configurationKey);
        }
    }
}