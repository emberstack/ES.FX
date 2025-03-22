using System.Reflection;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ES.FX.Extensions.Newtonsoft.Json.Serialization;

/// <summary>
///     Used to resolve <see cref="JsonPropertyNameAttribute" /> decorated contracts
/// </summary>
[PublicAPI]
public class JsonPropertyNameContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        if (member.GetCustomAttribute<JsonPropertyNameAttribute>() is not { } propertyNameAttribute) return property;
        property.PropertyName = propertyNameAttribute.Name;
        return property;
    }
}