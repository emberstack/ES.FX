using System.Net;
using System.Text.Json;
using Microsoft.Kiota.Abstractions;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     Escape hatches for the places where the generated builders cannot express the live Zendesk wire format
///     (see <c>src/ES.FX.Zendesk/OpenApi/README.md</c>). Two distinct causes converge here: genuine spec
///     omissions (query parameters, sideloads and endpoints the published OAS does not model — recorded in the
///     README's spec-anomaly ledger) and generator limitations (OAS-modeled constructs Kiota collapses, e.g. the
///     cursor <c>page</c> deepObject parameters and the P5 <c>.json</c> item-path suffix — recorded///     README's known
///     generator hazards). These helpers extend a generated <see cref="RequestInformation" />ormation" />
///     rather than hand-building URLs, so path templating, encoding and the adapter pipeline stay intact.
/// </summary>
internal static class ZendeskKiotaRequests
{
    /// <summary>
    ///     Adds a query parameter that the generated request builder does not expose. The name must be
    ///     percent-encoded exactly as it should appear on the wire (e.g. <c>page%5Bsize%5D</c> for
    ///     <c>page[size]</c>). <c>null</c> — and blank/whitespace-only string — values are ignored: an empty
    ///     query value is never a meaningful filter here and Zendesk rejects some outright (e.g. an agent
    ///     passing <c>afterCursor=""</c> would otherwise emit <c>page[after]=</c> → 400
    ///     <c>"page[after] is not valid"</c>). Only genuinely-present values reach the wire.
    /// </summary>
    public static RequestInformation WithQuery(this RequestInformation request, string encodedName, object? value)
    {
        if (value is null || (value is string text && string.IsNullOrWhiteSpace(text))) return request;

        // Generated templates come in several shapes; the added parameter must join the EXISTING query
        // string exactly once. Traps: some templates carry a LITERAL query ('/uploads?filename={filename}')
        // or an '{&...}' continuation group ('/search?query={query}{&sort_by*}') — appending '{?name}' to
        // those would emit a second '?' and corrupt the URL server-side.
        var template = request.UrlTemplate!;
        if (template.Contains("{?", StringComparison.Ordinal))
            request.UrlTemplate = template.Replace("{?", "{?" + encodedName + ",", StringComparison.Ordinal);
        else if (template.Contains("{&", StringComparison.Ordinal))
            request.UrlTemplate = template.Replace("{&", "{&" + encodedName + ",", StringComparison.Ordinal);
        else if (template.Contains('?'))
            request.UrlTemplate = template + "{&" + encodedName + "}";
        else
            request.UrlTemplate = template + "{?" + encodedName + "}";
        request.QueryParameters.Add(encodedName, value);
        return request;
    }

    /// <summary>
    ///     Adds Zendesk cursor-pagination parameters (<c>page[size]</c> / <c>page[after]</c>) to a request whose
    ///     generated builder cannot emit them. Two legitimate causes, verified per call site: the spec omits
    ///     cursor paging entirely (a spec-anomaly ledger row in <c>src/ES.FX.Zendesk/OpenApi/README.md</c>), or
    ///     the spec DOES model it — as the <c>CursorPaginationPage</c>/<c>DualPaginationPage</c> deepObject
    ///     <c>page</c> parameter — but Kiota collapses that to a plain <c>int? Page</c> property that cannot
    ///     produce the bracketed pair (the README's known generator hazard).
    /// </summary>
    public static RequestInformation WithCursorPagination(this RequestInformation request, int? pageSize,
        string? afterCursor) =>
        request.WithQuery("page%5Bsize%5D", pageSize).WithQuery("page%5Bafter%5D", afterCursor);

    /// <summary>
    ///     Adds the <c>include</c> sideload parameter to a request whose generated builder does not expose it.
    /// </summary>
    public static RequestInformation WithInclude(this RequestInformation request, string? include) =>
        request.WithQuery("include", include);

    /// <summary>
    ///     Re-appends the <c>.json</c> suffix to the request's path. The Help Center normalized spec carries the
    ///     suffix on every path (patch P5 — live testing produced HTTP 415 on extension-less Help Center paths,
    ///     even with JSON headers), but Kiota collapses <c>{id}.json</c> leaf paths into extension-less item
    ///     nodes, silently defeating the patch for the article/section/category item builders (see the known
    ///     generator hazards in <c>src/ES.FX.Zendesk/OpenApi/README.md</c>). The suffix is inserted before any
    ///     query block so template expansion stays intact.
    /// </summary>
    public static RequestInformation WithJsonSuffix(this RequestInformation request)
    {
        var template = request.UrlTemplate!;
        var queryStart = template.IndexOf("{?", StringComparison.Ordinal);
        if (queryStart < 0) queryStart = template.IndexOf('?');
        request.UrlTemplate = queryStart < 0 ? template + ".json" : template.Insert(queryStart, ".json");
        return request;
    }

    /// <summary>
    ///     Sends the request and parses the raw response body as JSON. For endpoints (or response shapes) the
    ///     published spec does not model at all, where no generated model type exists.
    /// </summary>
    public static async Task<JsonElement> SendForJsonAsync(this IRequestAdapter adapter, RequestInformation request,
        CancellationToken cancellationToken = default)
    {
        var stream = await adapter.SendPrimitiveAsync<Stream>(request, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (stream is null) return default;

        await using (stream.ConfigureAwait(false))
        {
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return document.RootElement.Clone();
        }
    }

    /// <summary>
    ///     Like <see cref="SendForJsonAsync" /> but also surfaces the HTTP status code, which the request adapter
    ///     normally discards — for the upsert endpoints whose <c>200</c>-vs-<c>201</c> distinction carries the
    ///     <c>created: true|false</c> signal. A <see cref="NativeResponseHandler" /> is attached via
    ///     <see cref="ResponseHandlerOption" /> so the raw <see cref="HttpResponseMessage" /> reaches this method;
    ///     the HTTP handler chain (resilience, response guard) is unaffected — it runs before the handler sees
    ///     the final response.
    /// </summary>
    /// <remarks>
    ///     The native handler also bypasses Kiota's error mapping, so non-success statuses are re-guarded here
    ///     with <see cref="ZendeskResponseGuard" /> — the same logic the innermost HTTP handler applies — keeping
    ///     the error semantics (typed <see cref="ZendeskApiException" />, bounded body prefix, <c>Retry-After</c>)
    ///     identical on every path. Only retry-exhausted <c>408</c>/<c>429</c>/<c>5xx</c> responses can actually
    ///     reach that guard call: everything else already threw inside the handler chain.
    /// </remarks>
    public static async Task<(HttpStatusCode StatusCode, JsonElement Body)> SendForJsonWithStatusAsync(
        this IRequestAdapter adapter, RequestInformation request, CancellationToken cancellationToken = default)
    {
        var responseHandler = new NativeResponseHandler();
        request.AddRequestOptions([new ResponseHandlerOption { ResponseHandler = responseHandler }]);
        await adapter.SendPrimitiveAsync<Stream>(request, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (responseHandler.Value is not HttpResponseMessage response)
            throw new InvalidOperationException("The request adapter completed without an HTTP response.");

        using (response)
        {
            await ZendeskResponseGuard.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NoContent || response.Content.Headers.ContentLength is 0)
                return (response.StatusCode, default);

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return (response.StatusCode, document.RootElement.Clone());
            }
        }
    }
}