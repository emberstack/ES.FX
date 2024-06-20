using System.Text;
using MassTransit;

namespace ES.FX.Additions.MassTransit.Formatters;

/// <summary>
///     Provides a way to aggregate multiple prefix providers to format the entity name
/// </summary>
/// <param name="entityNameFormatter">The base <see cref="IEntityNameFormatter" /></param>
/// <param name="prefixProviders"> The prefix providers to aggregate</param>
/// <param name="separator"> String separator added to prefixes</param>
public class AggregatePrefixEntityNameFormatter(
    IEntityNameFormatter entityNameFormatter,
    string? separator = null,
    params Func<Type, string?>[] prefixProviders) : IEntityNameFormatter
{
    /// <inheritdoc cref="IEntityNameFormatter.FormatEntityName{TMessage}" />
    public string FormatEntityName<TMessage>()
    {
        var stringBuilder = new StringBuilder();
        foreach (var prefixProvider in prefixProviders)
        {
            var format = prefixProvider(typeof(TMessage));
            if (string.IsNullOrWhiteSpace(format)) continue;
            stringBuilder.Append(format);
            if (!string.IsNullOrWhiteSpace(separator))
                stringBuilder.Append(separator);
        }

        stringBuilder.Append(entityNameFormatter.FormatEntityName<TMessage>());
        return stringBuilder.ToString();
    }
}