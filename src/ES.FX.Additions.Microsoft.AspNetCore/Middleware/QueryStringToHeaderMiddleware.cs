﻿using JetBrains.Annotations;
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

    [PublicAPI]
    public async Task InvokeAsync(HttpContext context)
    {
        var pairs = context.Request.Query
            .Where(s => s.Key.StartsWith(Prefix, StringComparison.InvariantCultureIgnoreCase))
            .ToList();

        foreach (var pair in pairs)
        {
            var headerKey = pair.Key[Prefix.Length..];
            if (string.IsNullOrWhiteSpace(headerKey)) continue;
            context.Request.Headers[headerKey] = pair.Value;
        }

        await next(context);
    }
}