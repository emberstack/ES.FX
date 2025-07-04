using ES.FX.ComponentModel.DataAnnotations;
using MassTransit;

namespace ES.FX.Additions.MassTransit.Formatters;

/// <summary>
///     Default endpoint name formatter that uses the <see cref="KindAttribute" /> to format the endpoint name. Uses
///     <see cref="DefaultEndpointNameFormatter" /> as the base formatter
/// </summary>
/// <param name="joinSeparator">Define the join separator between the words</param>
/// <param name="prefix">Prefix to start the name, should match the casing of the formatter (such as Dev or PreProd)</param>
/// <param name="includeNamespace">If true, the namespace is included in the name</param>
public class KindEndpointNameFormatter(
    string joinSeparator = "",
    string prefix = "",
    bool includeNamespace = false)
    : DefaultEndpointNameFormatter(joinSeparator, prefix, includeNamespace)
{
    protected override string FormatName(Type type)
    {
        var kind = KindAttribute.For(type);
        if (string.IsNullOrWhiteSpace(kind)) return base.FormatName(type);
        return string.IsNullOrWhiteSpace(Prefix) ? kind : $"{Prefix}{kind}";
    }
}