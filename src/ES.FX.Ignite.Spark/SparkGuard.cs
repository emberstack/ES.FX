using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Spark;

public static class SparkGuard
{
    public static void GuardSparkConfiguration(this IHostApplicationBuilder builder, string key, string message)
    {
        var propertyKey = $"{nameof(SparkGuard)}-{key}";
        if (builder.Properties.ContainsKey(propertyKey))
            throw new SparkReconfigurationNotSupportedException(message);

        builder.Properties.Add(propertyKey, string.Empty);
    }
}