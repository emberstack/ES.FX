using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ES.FX.OpenData.Currencies.ISO4217.Internal;

/// <summary>Reads this package's embedded JSON via a source-generated <see cref="JsonTypeInfo{T}" /> (AOT/trim-safe).</summary>
internal static class OpenDataResources
{
    public static T DeserializeJson<T>(Assembly assembly, string logicalName, JsonTypeInfo<T> typeInfo)
    {
        using var stream = assembly.GetManifestResourceStream(logicalName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{logicalName}' was not found in assembly '{assembly.GetName().Name}'.");
        return JsonSerializer.Deserialize(stream, typeInfo)
               ?? throw new InvalidOperationException($"Embedded JSON resource '{logicalName}' deserialized to null.");
    }
}