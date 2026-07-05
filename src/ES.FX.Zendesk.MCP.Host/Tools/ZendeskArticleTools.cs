using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.HelpCenter;
using ES.FX.Zendesk.MCP.Host.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using ArticleItemBuilder = ES.FX.Zendesk.HelpCenter.Api.V2.Help_center.Articles.Item.Article_ItemRequestBuilder;
using CategoryItemBuilder = ES.FX.Zendesk.HelpCenter.Api.V2.Help_center.Categories.Item.Category_ItemRequestBuilder;
using LocaleCategoryItemBuilder =
    ES.FX.Zendesk.HelpCenter.Api.V2.Help_center.Item.Categories.Item.Category_ItemRequestBuilder;
using LocaleSectionItemBuilder =
    ES.FX.Zendesk.HelpCenter.Api.V2.Help_center.Item.Sections.Item.Section_ItemRequestBuilder;
using SectionItemBuilder = ES.FX.Zendesk.HelpCenter.Api.V2.Help_center.Sections.Item.Section_ItemRequestBuilder;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for the Zendesk Help Center knowledge base. Namespaced <c>articles_*</c>.
/// </summary>
/// <remarks>
///     Every read returns the raw wire JSON via <see cref="ZendeskKiotaRequests.SendForJsonAsync" /> instead of
///     round-tripping the generated Help Center models: the published Help Center spec types every id as a 32-bit
///     integer, so deserializing a real response (article/section/category ids are 12–13 digits) through the
///     generated models would fail. The generated request builders are still used to build each request, keeping
///     paths, templating and encoding intact; item builders are constructed directly (their generated indexers are
///     likewise <c>int</c>-typed) with the id carried as a <c>long</c> path parameter. Pagination and sideload
///     query parameters the live API accepts but the spec omits are added via the
///     <see cref="ZendeskKiotaRequests" /> escape hatches. List/search responses are then projected through
///     <see cref="ZendeskLean" /> into the uniform lean envelope — summary rows by default, complete records via
///     <c>detail:'full'</c> or the per-record <c>*_get</c> tools. Search never returns article bodies (the
///     snippet is the relevance signal; <c>articles_get</c> is the body sink, with its own plain-text conversion
///     and <c>maxBodyChars</c> cap).
/// </remarks>
[McpServerToolType]
public sealed class ZendeskArticleTools(
    ZendeskHelpCenterApiClient helpCenter,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The uniform <c>detail</c> parameter description shared by the list tools.</summary>
    private const string DetailDescription =
        "detail: 'summary' (default, lean triage rows) | 'full' (complete records minus null fields and API links).";

    /// <summary>Full-text searches Help Center knowledge base articles.</summary>
    [McpServerTool(Name = "articles_search", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Full-text search Help Center articles that answer a customer's question. Rows are lean summaries (id, " +
        "title, snippet, html_url, section_id, locale, promoted, label_names, updated_at); <em> markers in " +
        "'snippet' highlight matched terms (relevance signal). Body NEVER returned here — read via articles_get. " +
        "At least one of query / sectionId / categoryId / labelNames is required; locale alone is not sufficient. " +
        "perPage default 10 (max 100); total match count in 'count'. Read-only.")]
    public Task<JsonElement> Search(
        [Description(
            "Question/keywords; matches article title, body, labels. Wrap in double quotes for exact-phrase match " +
            "(e.g. \"carrot potato\"). Optional when a sectionId / categoryId / labelNames filter is supplied; " +
            "at least one of those or query must be present (locale alone is not sufficient).")]
        string? query = null,
        [Description(
            "OMIT to search the tenant default locale (invalid/non-enabled values also fall back to it); pass the " +
            "requester's locale (e.g. en-us) to keep answers in-language, or \"*\" for all locales.")]
        string? locale = null,
        [Description("Restricts results to this section id (see articles_sections_list).")]
        long? sectionId = null,
        [Description("Restricts results to this category id (see articles_categories_list).")]
        long? categoryId = null,
        [Description(
            "Restricts results to articles carrying ALL of these labels — comma-separated label names " +
            "(e.g. \"printer,setup\"). Case-insensitive; matches an article's label_names.")]
        string? labelNames = null,
        [Description(
            "Sort field: \"created_at\" | \"updated_at\" | \"position\". OMIT to keep the default relevance " +
            "ranking (the strongest signal for a question).")]
        string? sortBy = null,
        [Description("1-based page number.")] int? page = null,
        [Description(
            "Results per page (min 1, max 100; default 10). Relevance-ranked. Total in 'count'; " +
            "'has_more'/'next_page' drive paging.")]
        int? perPage = 10,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            // The generated search builder exposes query/locale, but not the offset pagination the live Help
            // Center search endpoint accepts (recorded spec anomaly — see the ledger in
            // src/ES.FX.Zendesk/OpenApi/README.md) — extend the generated request instead.
            var request = helpCenter.Api.V2.Help_center.Articles.SearchJson.ToGetRequestInformation(configuration =>
                {
                    if (!string.IsNullOrWhiteSpace(query)) configuration.QueryParameters.Query = query;
                    if (!string.IsNullOrWhiteSpace(locale)) configuration.QueryParameters.Locale = locale;
                })
                .WithQuery("section", sectionId)
                .WithQuery("category", categoryId)
                .WithQuery("label_names", labelNames)
                .WithQuery("sort_by", sortBy)
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            // Search rows are ALWAYS the lean article summary — there is no detail escalation here because the
            // body is deliberately never returned by search (articles_get is the body sink). Help Center search
            // returns articles in a 'results' array, so the rows dispatch onto the article summary shape.
            return ZendeskLean.BuildOffsetListEnvelope(json, "results", page, ZendeskDetail.Summary,
                MaxResponseChars("articles_search"), itemShapeName: "articles");
        });

    /// <summary>Returns a single Help Center article including its body.</summary>
    [McpServerTool(Name = "articles_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Single Help Center article by id — full-detail record (null fields and API self-links omitted; absent " +
        "field means null/empty) including body. Use after articles_search to read the complete answer. Body " +
        "converted to plain text by default (bodyFormat:'plain', fraction of HTML tokens); bodyFormat:'html' " +
        "for original markup. Body capped at maxBodyChars (default 4000, applied AFTER bodyFormat conversion); " +
        "a capped body ends with a " +
        "marker naming the recovery (maxBodyChars:0 = no limit) and the article's html_url permalink. Read-only.")]
    public Task<JsonElement> Read(
        [Description("Numeric Help Center article id.")]
        long id,
        [Description(
            "\"plain\" (default, MCP-side HTML->text, fraction of tokens) | \"html\" (original markup).")]
        string? bodyFormat = "plain",
        CancellationToken cancellationToken = default,
        [Description(
            "Body char cap, applied AFTER bodyFormat conversion (default 4000; 0 = no limit). Capped body ends " +
            "with a marker naming the re-call (maxBodyChars:0) and the article's html_url.")]
        int maxBodyChars = 4000)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var plainBody = ParseBodyFormat(bodyFormat);
            if (maxBodyChars < 0)
                throw new McpException(
                    $"Invalid maxBodyChars value '{maxBodyChars}'. Pass a positive character cap, or 0 for no limit.");

            // WithJsonSuffix: Kiota drops the P5 '.json' suffix on {id} item paths; extension-less Help Center
            // paths 415 on live tenants (see the generator hazards in src/ES.FX.Zendesk/OpenApi/README.md).
            var request = new ArticleItemBuilder(ItemPathParameters("article_%2Did", id), requestAdapter)
                .ToGetRequestInformation()
                .WithJsonSuffix();
            var envelope = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            var article = Unwrap(envelope, "article")
                          ?? throw new McpException($"Zendesk article '{id}' was not found.");
            return ProjectArticle(ZendeskLean.ToFullView(article), plainBody, maxBodyChars);
        });

    /// <summary>Lists Help Center articles, optionally scoped to a section.</summary>
    [McpServerTool(Name = "articles_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "List Help Center articles, optionally scoped to a section — browse structurally (vs by relevance, which " +
        "is articles_search). Rows are lean summaries (id, title, html_url, section_id, locale, draft, promoted, " +
        "label_names, updated_at; no body) — detail:'full' for complete records including bodies, or articles_get " +
        "for one. Cursor pagination: pageSize default 25 (max 100); response's has_more/after_cursor drive " +
        "continuation. Read-only.")]
    public Task<JsonElement> List(
        [Description("Scope locale, e.g. \"en-us\".")]
        string? locale = null,
        [Description("Lists only articles in this section (see articles_sections_list).")]
        long? sectionId = null,
        [Description("Cursor page size (default 25; API caps at 100/page).")]
        int? pageSize = 25,
        [Description(
            "Continuation cursor for NEXT page — OMIT for first page. When present, the opaque token copied " +
            "verbatim from previous response's after_cursor; not a page number, must not be guessed or passed " +
            "empty (invalid cursor -> 400).")]
        string? afterCursor = null,
        [Description(
            "Sideloads resolved inline as sibling arrays: \"users\" (author) | \"sections\" | \"categories\" | " +
            "\"translations\". \"translations\" is embedded per-article and body-heavy — summary rows exclude it " +
            "(detail:'full' to see it; expect a LARGE response, every translation carries a full body). Only " +
            "these four valid.")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var root = helpCenter.Api.V2.Help_center;
            var request = (NormalizeLocale(locale), sectionId) switch
            {
                (null, null) => root.ArticlesJson.ToGetRequestInformation(),
                ({ } scopedLocale, null) => root[scopedLocale].ArticlesJson.ToGetRequestInformation(),
                (null, { } scopedSectionId) => new SectionItemBuilder(
                        ItemPathParameters("section_%2Did", scopedSectionId), requestAdapter)
                    .ArticlesJson.ToGetRequestInformation(),
                ({ } scopedLocale, { } scopedSectionId) => new LocaleSectionItemBuilder(
                        ItemPathParameters("section_%2Did", scopedSectionId, scopedLocale), requestAdapter)
                    .ArticlesJson.ToGetRequestInformation()
            };
            // The generated list builders only expose sort/label filters — cursor pagination and sideloads are
            // accepted by the live API but missing from the spec (List Articles doc:
            // https://developer.zendesk.com/api-reference/help_center/help-center-api/articles/; ledger rows in
            // src/ES.FX.Zendesk/OpenApi/README.md).
            request.WithCursorPagination(pageSize, afterCursor).WithInclude(JoinInclude(include));
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "articles", parsedDetail,
                MaxResponseChars("articles_list"));
        });

    /// <summary>Lists Help Center sections, optionally scoped to a category.</summary>
    [McpServerTool(Name = "articles_sections_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "List Help Center sections (middle tier of category -> section -> article hierarchy), optionally scoped " +
        "to a category — use returned section ids to browse articles via articles_list. Rows are lean summaries " +
        "(id, name, html_url, 200-char description excerpt, category_id, parent_section_id, position, updated_at) " +
        "— detail:'full' for complete records, or articles_sections_get for one. perPage default 30 (max 100). " +
        "Read-only.")]
    public Task<JsonElement> Sections(
        [Description("Scope locale, e.g. \"en-us\".")]
        string? locale = null,
        [Description("Lists only sections in this category (see articles_categories_list).")]
        long? categoryId = null,
        [Description("1-based page number.")] int? page = null,
        [Description("Results per page (default 30, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 30,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var root = helpCenter.Api.V2.Help_center;
            var request = (NormalizeLocale(locale), categoryId) switch
            {
                (null, null) => root.SectionsJson.ToGetRequestInformation(),
                ({ } scopedLocale, null) => root[scopedLocale].SectionsJson.ToGetRequestInformation(),
                (null, { } scopedCategoryId) => new CategoryItemBuilder(
                        ItemPathParameters("category_%2Did", scopedCategoryId), requestAdapter)
                    .SectionsJson.ToGetRequestInformation(),
                ({ } scopedLocale, { } scopedCategoryId) => new LocaleCategoryItemBuilder(
                        ItemPathParameters("category_%2Did", scopedCategoryId, scopedLocale), requestAdapter)
                    .SectionsJson.ToGetRequestInformation()
            };
            // The generated section list builders expose no pagination — the live API accepts offset paging
            // (recorded spec anomaly — see the ledger in src/ES.FX.Zendesk/OpenApi/README.md).
            request.WithQuery("page", page).WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "sections", page, parsedDetail,
                MaxResponseChars("articles_sections_list"));
        });

    /// <summary>Returns a single Help Center section by id.</summary>
    [McpServerTool(Name = "articles_sections_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Single Help Center section by id — name/description behind an article's section_id. Null fields and API " +
        "self-links omitted; absent field means null/empty. Read-only.")]
    public Task<JsonElement> SectionRead(
        [Description("Numeric Help Center section id.")]
        long id,
        [Description("Locale segment, e.g. \"en-us\".")]
        string? locale = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            // WithJsonSuffix: Kiota drops the P5 '.json' suffix on {id} item paths; extension-less Help Center
            // paths 415 on live tenants (see the generator hazards in src/ES.FX.Zendesk/OpenApi/README.md).
            var request = (NormalizeLocale(locale) is { } scopedLocale
                    ? new LocaleSectionItemBuilder(ItemPathParameters("section_%2Did", id, scopedLocale),
                            requestAdapter)
                        .ToGetRequestInformation()
                    : new SectionItemBuilder(ItemPathParameters("section_%2Did", id), requestAdapter)
                        .ToGetRequestInformation())
                .WithJsonSuffix();
            var envelope = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return Unwrap(envelope, "section") is { } section
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(section), "articles_sections_get",
                    MaxResponseChars("articles_sections_get"), "Record exceeds the response budget.")
                : throw new McpException($"Zendesk Help Center section '{id}' was not found.");
        });

    /// <summary>Lists Help Center categories.</summary>
    [McpServerTool(Name = "articles_categories_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "List Help Center categories (top tier of category -> section -> article hierarchy) — use returned " +
        "category ids to browse sections via articles_sections_list. Rows are lean summaries (id, name, html_url, " +
        "200-char description excerpt, position, updated_at) — detail:'full' for complete records, or " +
        "articles_categories_get for one. perPage default 30 (max 100). Read-only.")]
    public Task<JsonElement> Categories(
        [Description("Scope locale, e.g. \"en-us\".")]
        string? locale = null,
        [Description("1-based page number.")] int? page = null,
        [Description("Results per page (default 30, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 30,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var root = helpCenter.Api.V2.Help_center;
            var request = NormalizeLocale(locale) is { } scopedLocale
                ? root[scopedLocale].CategoriesJson.ToGetRequestInformation()
                : root.CategoriesJson.ToGetRequestInformation();
            // The generated category list builders expose no pagination — the live API accepts offset paging
            // (recorded spec anomaly — see the ledger in src/ES.FX.Zendesk/OpenApi/README.md).
            request.WithQuery("page", page).WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "categories", page, parsedDetail,
                MaxResponseChars("articles_categories_list"));
        });

    /// <summary>Returns a single Help Center category by id.</summary>
    [McpServerTool(Name = "articles_categories_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Single Help Center category by id — name/description behind a section's category_id. Null fields and API " +
        "self-links omitted; absent field means null/empty. Read-only.")]
    public Task<JsonElement> CategoryRead(
        [Description("Numeric Help Center category id.")]
        long id,
        [Description("Locale segment, e.g. \"en-us\".")]
        string? locale = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            // WithJsonSuffix: Kiota drops the P5 '.json' suffix on {id} item paths; extension-less Help Center
            // paths 415 on live tenants (see the generator hazards in src/ES.FX.Zendesk/OpenApi/README.md).
            var request = (NormalizeLocale(locale) is { } scopedLocale
                    ? new LocaleCategoryItemBuilder(ItemPathParameters("category_%2Did", id, scopedLocale),
                        requestAdapter).ToGetRequestInformation()
                    : new CategoryItemBuilder(ItemPathParameters("category_%2Did", id), requestAdapter)
                        .ToGetRequestInformation())
                .WithJsonSuffix();
            var envelope = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return Unwrap(envelope, "category") is { } category
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(category), "articles_categories_get",
                    MaxResponseChars("articles_categories_get"), "Record exceeds the response budget.")
                : throw new McpException($"Zendesk Help Center category '{id}' was not found.");
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);

    /// <summary>
    ///     Parses and validates the article <c>bodyFormat</c> parameter, returning <c>true</c> for the plain-text
    ///     conversion. Accepts <c>plain</c> (the default) and <c>html</c>, case-insensitively; a missing/blank
    ///     value falls back to <c>plain</c>. Anything else is rejected with an <see cref="McpException" /> naming
    ///     the allowed values — never silently coerced.
    /// </summary>
    private static bool ParseBodyFormat(string? bodyFormat)
    {
        if (string.IsNullOrWhiteSpace(bodyFormat)) return true;
        return bodyFormat.Trim().ToLowerInvariant() switch
        {
            "plain" => true,
            "html" => false,
            _ => throw new McpException(
                $"Invalid bodyFormat value '{bodyFormat}'. Allowed values: 'plain' (default) or 'html'.")
        };
    }

    /// <summary>
    ///     Projects an article's <c>body</c> for the response: the <c>bodyFormat:'plain'</c> HTML→text conversion
    ///     first (token economy), then the <paramref name="maxBodyChars" /> cap (0 = no limit) applied AFTER the
    ///     conversion — capping the HTML before converting would waste the budget on markup. A capped body ends
    ///     with a marker naming the exact recovery (see <see cref="BodyRecovery" />).
    /// </summary>
    private static JsonElement ProjectArticle(JsonElement fullView, bool plainBody, int maxBodyChars)
    {
        var article = (JsonObject)JsonNode.Parse(fullView.GetRawText())!;
        if (article["body"] is not JsonValue bodyValue || !bodyValue.TryGetValue(out string? body) ||
            string.IsNullOrEmpty(body))
            return fullView;

        if (plainBody) body = ZendeskLean.HtmlToPlainText(body);
        if (maxBodyChars > 0) body = ZendeskLean.TruncateWithMarker(body, maxBodyChars, BodyRecovery(article));
        article["body"] = body;
        return JsonSerializer.SerializeToElement(article);
    }

    /// <summary>
    ///     The body-cap recovery recipe: the exact re-call that returns the untruncated body, plus the article's
    ///     <c>html_url</c> human permalink (the full rendered article) when Zendesk supplied one.
    /// </summary>
    private static string BodyRecovery(JsonObject article) =>
        article["html_url"] is JsonValue htmlUrlValue && htmlUrlValue.TryGetValue(out string? htmlUrl) &&
        !string.IsNullOrEmpty(htmlUrl)
            ? $"re-call with maxBodyChars:0 (0 = no limit) for the full body, or read it at {htmlUrl}"
            : "re-call with maxBodyChars:0 (0 = no limit) for the full body";

    /// <summary>
    ///     Builds the path-parameter dictionary for a directly constructed generated item builder. The generated
    ///     Help Center indexers take <c>int</c> ids (the spec omits the int64 format), so the id is supplied here
    ///     as a <c>long</c> instead; the locale, when present, is escaped by the URL template expansion — matching
    ///     the escaping the retired client applied.
    /// </summary>
    private Dictionary<string, object> ItemPathParameters(string idKey, long id, string? locale = null)
    {
        var parameters = new Dictionary<string, object> { ["baseurl"] = requestAdapter.BaseUrl! };
        if (locale is not null) parameters["locale"] = locale;
        parameters[idKey] = id;
        return parameters;
    }

    /// <summary>
    ///     Unwraps a single-resource envelope property (e.g. <c>{ "article": {...} }</c>), or <c>null</c> when the
    ///     property is missing or not an object — the caller raises the tool's not-found <see cref="McpException" />.
    /// </summary>
    private static JsonElement? Unwrap(JsonElement envelope, string property) =>
        envelope.ValueKind is JsonValueKind.Object &&
        envelope.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.Object
            ? value
            : null;

    /// <summary>Treats blank locales as absent, matching the retired client's path building.</summary>
    private static string? NormalizeLocale(string? locale) => string.IsNullOrWhiteSpace(locale) ? null : locale;

    /// <summary>
    ///     Builds the flat comma-separated sideload value (e.g. <c>users,sections</c>), or <c>null</c> when nothing
    ///     is requested — matching the omit-when-empty semantics of the retired client.
    /// </summary>
    private static string? JoinInclude(string[]? include)
    {
        if (include is null || include.Length == 0) return null;
        var joined = string.Join(',', include);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}