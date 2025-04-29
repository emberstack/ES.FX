using System.Net;
using JetBrains.Annotations;

namespace ES.FX.Net.Extensions;

[PublicAPI]
public static class WebProxyExtensions
{
    /// <summary>
    /// Builds an <see cref="IWebProxy"/> instance from the specified <see cref="BasicHttpProxyOptions"/>.
    /// </summary>
    /// <param name="options">The proxy options to use for building the proxy.</param>
    /// <returns>A configured <see cref="IWebProxy"/> instance, or <c>null</c> if no address is specified.</returns>
    public static IWebProxy? BuildBasicHttpProxy(this BasicHttpProxyOptions? options)
    {
        if (options == null || string.IsNullOrWhiteSpace(options.Address))
            return null;

        var proxy = new WebProxy(options.Address)
        {
            BypassProxyOnLocal = options.BypassProxyOnLocal,
            BypassList = options.BypassList ?? [],
            UseDefaultCredentials = options.UseDefaultCredentials,
            Credentials = options.Credentials is not null
                ? new NetworkCredential(
                    options.Credentials.UserName,
                    options.Credentials.Password,
                    options.Credentials.Domain)
                : null
        };

        return proxy;
    }
}