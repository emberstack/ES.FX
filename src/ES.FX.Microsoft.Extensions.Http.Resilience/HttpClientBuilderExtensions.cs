using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace ES.FX.Microsoft.Extensions.Http.Resilience;

/// <summary>
///     Provides extension methods to <see cref="IHttpClientBuilder" />
/// </summary>
public static class HttpClientBuilderExtensions
{
#pragma warning disable EXTEXP0001
    /// <summary>
    ///     Remove already configured resilience handlers
    /// </summary>
    /// <param name="builder">The builder instance.</param>
    /// <returns>The value of <paramref name="builder" />.</returns>
    //TODO: Remove this method when the issue is fixed: https://github.com/dotnet/extensions/issues/5021
    [PublicAPI]
    public static IHttpClientBuilder ClearResilienceHandlers(this IHttpClientBuilder builder)
    {
        builder.ConfigureAdditionalHttpMessageHandlers(static (handlers, _) =>
        {
            for (var i = 0; i < handlers.Count;)
            {
                if (handlers[i] is ResilienceHandler)
                {
                    handlers.RemoveAt(i);
                    continue;
                }

                i++;
            }
        });
        return builder;
    }
#pragma warning restore EXTEXP0001
}