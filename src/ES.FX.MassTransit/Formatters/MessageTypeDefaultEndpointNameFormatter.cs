﻿using ES.FX.Contracts.Messaging;
using MassTransit;

namespace ES.FX.MassTransit.Formatters;

/// <summary>
///     Default endpoint name formatter that uses the message name attribute to format the endpoint name. Uses
///     <see cref="DefaultEndpointNameFormatter" /> as the base formatter
/// </summary>
/// <param name="joinSeparator">Define the join separator between the words</param>
/// <param name="prefix">Prefix to start the name, should match the casing of the formatter (such as Dev or PreProd)</param>
/// <param name="includeNamespace">If true, the namespace is included in the name</param>
public class MessageTypeDefaultEndpointNameFormatter(
    string joinSeparator = "",
    string prefix = "",
    bool includeNamespace = false)
    : DefaultEndpointNameFormatter(joinSeparator, prefix, includeNamespace)
{
    protected override string FormatName(Type message)
    {
        var type = MessageTypeAttribute.TypeFor(message);
        if (string.IsNullOrWhiteSpace(type)) return base.FormatName(message);
        return string.IsNullOrWhiteSpace(Prefix) ? type : $"{Prefix}{type}";
    }
}