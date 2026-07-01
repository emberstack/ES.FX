using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace ES.FX.Additions.Microsoft.AspNetCore.Middleware;

/// <summary>
///     Middleware to convert query string parameters to headers, when the query string key starts with
///     <see cref="Prefix" />
/// </summary>
public class QueryStringToHeaderMiddleware(RequestDelegate next)
{
    /// <summary>
    ///     Prefix to identify the query string parameters that should be converted to headers
    /// </summary>
    public const string Prefix = "X-Header-";

    /// <summary>
    ///     Copies query string parameters whose keys start with <see cref="Prefix" /> into request headers,
    ///     skipping pairs with invalid header names or values containing control characters, then invokes the
    ///     next middleware
    /// </summary>
    /// <param name="context">The <see cref="HttpContext" /> for the current request</param>
    /// <returns>A <see cref="Task" /> that represents the completion of request processing</returns>
    [PublicAPI]
    public async Task InvokeAsync(HttpContext context)
    {
        var pairs = context.Request.Query
            .Where(s => s.Key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase));

        foreach (var pair in pairs)
        {
            var headerKey = pair.Key[Prefix.Length..];
            if (string.IsNullOrWhiteSpace(headerKey) || !IsValidHeaderName(headerKey)) continue;
            if (pair.Value.Any(static value => value.AsSpan().IndexOfAny('\0', '\r', '\n') >= 0)) continue;
            context.Request.Headers[headerKey] = pair.Value;
        }

        await next(context);
    }

    private static bool IsValidHeaderName(string name) => name.All(static c => c switch
    {
        >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' => true,
        '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~' => true,
        _ => false
    });
}