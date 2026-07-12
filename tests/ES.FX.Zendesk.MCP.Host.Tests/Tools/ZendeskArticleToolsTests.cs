using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskArticleToolsTests
{
    private static ZendeskArticleTools CreateTools(ZendeskToolHarness harness, McpOptions? options = null) =>
        new(harness.CreateHelpCenterClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(options ?? new McpOptions()));

    [Fact]
    public async Task Search_Requests_HelpCenter_Search_And_Returns_Lean_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"results":[{"id":4403866997779,"url":"https://unit-test.zendesk.com/api/v2/help_center/en-us/articles/4403866997779.json",
              "html_url":"https://unit-test.zendesk.com/hc/en-us/articles/4403866997779","title":"Resetting your password",
              "snippet":"...reset your <em>password</em>...","locale":"en-us","section_id":360001234567,
              "author_id":360000654321,"label_names":["passwords"],"promoted":false,
              "created_at":"2026-01-01T00:00:00Z","updated_at":"2026-02-01T00:00:00Z",
              "body":"<p>full body that search must not surface</p>","result_type":"article"}],
             "count":1,"page":2,"page_count":5,
             "next_page":"https://unit-test.zendesk.com/api/v2/help_center/articles/search.json?page=3","previous_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Search("password reset", "en-us", page: 2, perPage: 25,
            cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/help_center/articles/search.json", request.Path);
        Assert.Contains("query=password%20reset", request.Query);
        Assert.Contains("locale=en-us", request.Query);
        // page/per_page are offset paging the live search endpoint accepts but the generated builder omits.
        // ("&page=" rather than "page=" — the latter is a substring of "per_page=".)
        Assert.Contains("&page=2", request.Query);
        Assert.Contains("per_page=25", request.Query);

        // The lean envelope: metadata first, article summary rows in 'items'. next_page is a computed page
        // NUMBER ((request page 2) + 1) — Zendesk's URL strings are never parsed or echoed.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.Equal(3, result.GetProperty("next_page").GetInt32());
        var article = result.GetProperty("items")[0];
        Assert.Equal(4403866997779, article.GetProperty("id").GetInt64()); // real Help Center ids exceed int32
        Assert.Equal("Resetting your password", article.GetProperty("title").GetString());
        // The snippet keeps its <em> markers — the cheap relevance signal the tool guidance documents.
        Assert.Equal("...reset your <em>password</em>...", article.GetProperty("snippet").GetString());
        // html_url (the human permalink / KB citation) is always kept; the API self-link is not.
        Assert.Equal("https://unit-test.zendesk.com/hc/en-us/articles/4403866997779",
            article.GetProperty("html_url").GetString());
        Assert.Equal(360001234567, article.GetProperty("section_id").GetInt64());
        Assert.Equal("en-us", article.GetProperty("locale").GetString());
        Assert.Equal("passwords", article.GetProperty("label_names")[0].GetString());
        Assert.Equal("2026-02-01T00:00:00Z", article.GetProperty("updated_at").GetString());
        // The body is NEVER returned by search (there is no detail escalation here) — articles_get is the sink.
        Assert.False(article.TryGetProperty("body", out _));
        Assert.False(article.TryGetProperty("url", out _));
        // Rows are homogeneous articles, so the search result_type discriminator adds nothing and is dropped.
        Assert.False(article.TryGetProperty("result_type", out _));
    }

    [Fact]
    public async Task Search_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"results":[],"count":0}""");
        var tools = CreateTools(harness);

        await tools.Search("password", cancellationToken: TestContext.Current.CancellationToken);

        // Snippets make search rows heavier than entity rows — the default page is 10, explicit on the wire.
        Assert.Equal("?per_page=10&query=password", harness.Request.Query);
    }

    [Fact]
    public async Task Read_Requests_The_Article_And_Converts_The_Body_To_Plain_Text_By_Default()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"article":{"id":4403866997779,"url":"https://unit-test.zendesk.com/api/v2/help_center/en-us/articles/4403866997779.json",
             "title":"How to","body":"<p>step one</p><p>step two</p>","locale":"en-us","vote_sum":null}}
            """);
        var tools = CreateTools(harness);

        var article = await tools.Read(4403866997779, cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        // The '.json' suffix is curated-restored (WithJsonSuffix): Kiota drops it from {id} item paths and the
        // extension-less form 415s on live tenants (P5, src/ES.FX.Zendesk/OpenApi/README.md).
        Assert.Equal("/api/v2/help_center/articles/4403866997779.json", request.Path);
        Assert.Equal(4403866997779, article.GetProperty("id").GetInt64());
        // bodyFormat defaults to 'plain': MCP-side HTML→text conversion (block tags become newlines).
        Assert.Equal("step one\n\nstep two", article.GetProperty("body").GetString());
        // Full view: the API self-link and null-valued fields are omitted (absent = null/empty).
        Assert.False(article.TryGetProperty("url", out _));
        Assert.False(article.TryGetProperty("vote_sum", out _));
    }

    [Fact]
    public async Task Read_BodyFormat_Html_Keeps_The_Original_Markup()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"article":{"id":4403866997779,"title":"How to","body":"<p>steps</p>","locale":"en-us"}}""");
        var tools = CreateTools(harness);

        var article = await tools.Read(4403866997779, "html",
            TestContext.Current.CancellationToken);

        Assert.Equal("<p>steps</p>", article.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Read_Caps_The_Body_With_A_Marker_Naming_MaxBodyChars_And_The_Html_Url()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"article":{"id":4403866997779,"html_url":"https://unit-test.zendesk.com/hc/en-us/articles/4403866997779",
             "title":"How to","body":"<p>0123456789</p>"}}
            """);
        var tools = CreateTools(harness);

        var article = await tools.Read(4403866997779, cancellationToken: TestContext.Current.CancellationToken,
            maxBodyChars: 4);

        // The marker names the exact recovery: maxBodyChars:0 re-call plus the html_url human permalink.
        Assert.Equal(
            "0123…[truncated 6 chars — re-call with maxBodyChars:0 (0 = no limit) for the full body, or read " +
            "it at https://unit-test.zendesk.com/hc/en-us/articles/4403866997779]",
            article.GetProperty("body").GetString());
        // The permalink itself stays on the record — the marker's pointer must be reachable.
        Assert.Equal("https://unit-test.zendesk.com/hc/en-us/articles/4403866997779",
            article.GetProperty("html_url").GetString());
    }

    [Fact]
    public async Task Read_Applies_The_Body_Cap_After_The_Plain_Text_Conversion()
    {
        var harness = new ZendeskToolHarness();
        // The HTML source (29 chars) is over the cap; the converted plain text (11 chars) is not — capping
        // before converting would waste the budget on markup and truncate a body that actually fits.
        harness.EnqueueJson(
            """{"article":{"id":1,"title":"How to","body":"<div><p>hello world</p></div>"}}""");
        var tools = CreateTools(harness);

        var article = await tools.Read(1, cancellationToken: TestContext.Current.CancellationToken,
            maxBodyChars: 15);

        Assert.Equal("hello world", article.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Read_MaxBodyChars_Zero_Disables_The_Cap()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"article":{"id":1,"title":"How to","body":"<p>0123456789</p>"}}""");
        var tools = CreateTools(harness);

        var article = await tools.Read(1, cancellationToken: TestContext.Current.CancellationToken,
            maxBodyChars: 0);

        Assert.Equal("0123456789", article.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Read_Rejects_An_Unknown_BodyFormat_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(1, "markdown", TestContext.Current.CancellationToken));

        Assert.Contains("'plain'", exception.Message);
        Assert.Contains("'html'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Rejects_A_Negative_MaxBodyChars_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(1, cancellationToken: TestContext.Current.CancellationToken, maxBodyChars: -1));

        Assert.Contains("maxBodyChars", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Throws_When_Article_Is_Missing()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        // McpException so the MCP SDK surfaces the message verbatim instead of an opaque generic error.
        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(5, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Zendesk article '5' was not found.", exception.Message);
    }

    [Theory]
    [InlineData(null, null, "/api/v2/help_center/articles.json")]
    [InlineData("en-us", null, "/api/v2/help_center/en-us/articles.json")]
    [InlineData(null, 360001234567L, "/api/v2/help_center/sections/360001234567/articles.json")]
    [InlineData("en-us", 360001234567L, "/api/v2/help_center/en-us/sections/360001234567/articles.json")]
    public async Task List_Builds_The_Legacy_Path_For_Locale_And_Section(string? locale, long? sectionId,
        string expectedPath)
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"articles":[]}""");
        var tools = CreateTools(harness);

        await tools.List(locale, sectionId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, harness.Request.Method);
        Assert.Equal(expectedPath, harness.Request.Path);
    }

    [Fact]
    public async Task List_Requests_Cursor_Pagination_And_Include_And_Returns_The_Lean_Envelope()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"articles":[{"id":4403866997779,"title":"How to","body":"<p>full body</p>",
              "translations":[{"id":1,"locale":"de","body":"<p>ganzer Text</p>"}]}],
             "count":4,"next_page":null,"previous_page":null,
             "meta":{"has_more":true,"after_cursor":"cursor-2"},
             "users":[{"id":360000654321,"name":"Author"}],"sections":[{"id":360001234567,"name":"FAQ"}]}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List("en-us", 360001234567, 50, "cursor-1", ["users", "sections"],
            TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/help_center/en-us/sections/360001234567/articles.json", request.Path);
        Assert.Contains("page%5Bsize%5D=50", request.Query);
        Assert.Contains("page%5Bafter%5D=cursor-1", request.Query);
        Assert.Contains("include=users%2Csections", request.Query);
        // The lean envelope: metadata first, summary items, sideloads under their native names.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(4, result.GetProperty("count").GetInt32());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cursor-2", result.GetProperty("after_cursor").GetString());
        var article = result.GetProperty("items")[0];
        Assert.Equal(4403866997779, article.GetProperty("id").GetInt64());
        // The body-leak fix: summary rows carry neither the body nor the embedded translations sideload
        // (every translation carries a full article body) — detail:'full' is the escalation for both.
        Assert.False(article.TryGetProperty("body", out _));
        Assert.False(article.TryGetProperty("translations", out _));
        Assert.Equal("Author", result.GetProperty("users")[0].GetProperty("name").GetString());
        Assert.Equal("FAQ", result.GetProperty("sections")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task List_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"articles":[]}""");
        var tools = CreateTools(harness);

        await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        // The default page size is explicit on the wire — never left to the Help Center server default.
        Assert.Equal("?page%5Bsize%5D=25", harness.Request.Query);
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"articles":[{"id":4403866997779,"title":"How to","body":"<p>full body</p>",
              "translations":[{"id":1,"locale":"de","body":"<p>ganzer Text</p>"}],
              "url":"https://unit-test.zendesk.com/api/v2/help_center/en-us/articles/4403866997779.json",
              "author_id":null}],
             "meta":{"has_more":false}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var article = result.GetProperty("items")[0];
        // Full rows keep everything the summary shape strips — including the body and embedded translations...
        Assert.Equal("<p>full body</p>", article.GetProperty("body").GetString());
        Assert.Equal("de", article.GetProperty("translations")[0].GetProperty("locale").GetString());
        // ...but are still the full VIEW: API self-links and null-valued fields are gone.
        Assert.False(article.TryGetProperty("url", out _));
        Assert.False(article.TryGetProperty("author_id", out _));
    }

    [Fact]
    public async Task List_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.List(detail: "everything", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Theory]
    [InlineData(null, null, "/api/v2/help_center/sections.json")]
    [InlineData("en-us", null, "/api/v2/help_center/en-us/sections.json")]
    [InlineData(null, 360000111222L, "/api/v2/help_center/categories/360000111222/sections.json")]
    [InlineData("en-us", 360000111222L, "/api/v2/help_center/en-us/categories/360000111222/sections.json")]
    public async Task Sections_Builds_The_Legacy_Path_For_Locale_And_Category(string? locale, long? categoryId,
        string expectedPath)
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"sections":[]}""");
        var tools = CreateTools(harness);

        await tools.Sections(locale, categoryId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, harness.Request.Method);
        Assert.Equal(expectedPath, harness.Request.Path);
    }

    [Fact]
    public async Task Sections_Requests_Offset_Pagination_And_Returns_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"sections":[{"id":360001234567,"name":"FAQ","category_id":360000111222,"locale":"en-us",
              "url":"https://unit-test.zendesk.com/api/v2/help_center/en-us/sections/360001234567.json"}],
             "count":3,"next_page":"https://unit-test.zendesk.com/api/v2/help_center/sections.json?page=2","previous_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Sections("en-us", 360000111222, 1, 100, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/help_center/en-us/categories/360000111222/sections.json", request.Path);
        Assert.Contains("&page=1", request.Query);
        Assert.Contains("per_page=100", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(3, result.GetProperty("count").GetInt32());
        // next_page is a computed page NUMBER ((request page 1) + 1) — Zendesk's URL is never parsed or echoed.
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal(2, result.GetProperty("next_page").GetInt32());
        var section = result.GetProperty("items")[0];
        Assert.Equal(360001234567, section.GetProperty("id").GetInt64());
        Assert.Equal("FAQ", section.GetProperty("name").GetString());
        Assert.Equal(360000111222, section.GetProperty("category_id").GetInt64());
        // Summary rows are allowlisted — fields outside the section shape do not appear.
        Assert.False(section.TryGetProperty("locale", out _));
    }

    [Fact]
    public async Task Sections_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"sections":[]}""");
        var tools = CreateTools(harness);

        await tools.Sections(cancellationToken: TestContext.Current.CancellationToken);

        // Down from the old 100 default — explicit on the wire, never left to Zendesk's server default.
        Assert.Equal("?per_page=30", harness.Request.Query);
    }

    [Fact]
    public async Task Sections_Detail_Full_Returns_Unstripped_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"sections":[{"id":360001234567,"name":"FAQ","locale":"en-us","outdated":false,
              "url":"https://unit-test.zendesk.com/api/v2/help_center/en-us/sections/360001234567.json",
              "theme_template":null}],"count":1,"next_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Sections(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var section = result.GetProperty("items")[0];
        Assert.Equal("en-us", section.GetProperty("locale").GetString()); // the complete record...
        Assert.False(section.GetProperty("outdated").GetBoolean());
        Assert.False(section.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(section.TryGetProperty("theme_template", out _)); // ...and null-valued fields
    }

    [Fact]
    public async Task Sections_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Sections(detail: "verbose-ish", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task SectionRead_Requests_Locale_Scoped_Section_And_Returns_The_Full_View()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"section":{"id":360000222333,"name":"FAQ","locale":"en-us","description":null,
             "url":"https://unit-test.zendesk.com/api/v2/help_center/en-us/sections/360000222333.json"}}
            """);
        var tools = CreateTools(harness);

        var section = await tools.SectionRead(360000222333, "en-us", TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/help_center/en-us/sections/360000222333.json", harness.Request.Path);
        Assert.Equal(360000222333, section.GetProperty("id").GetInt64());
        Assert.Equal("FAQ", section.GetProperty("name").GetString());
        // Full view: the API self-link and null-valued fields are omitted (absent = null/empty).
        Assert.False(section.TryGetProperty("url", out _));
        Assert.False(section.TryGetProperty("description", out _));
    }

    [Fact]
    public async Task SectionRead_Uses_Flat_Path_Without_Locale()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"section":{"id":21,"name":"FAQ"}}""");
        var tools = CreateTools(harness);

        await tools.SectionRead(21, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/help_center/sections/21.json", harness.Request.Path);
    }

    [Fact]
    public async Task SectionRead_Escapes_The_Locale_Path_Segment()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"section":{"id":21,"name":"FAQ"}}""");
        var tools = CreateTools(harness);

        await tools.SectionRead(21, "en us", TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/help_center/en%20us/sections/21.json", harness.Request.Path);
    }

    [Fact]
    public async Task SectionRead_Throws_When_Section_Is_Missing()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        // McpException so the MCP SDK surfaces the message verbatim instead of an opaque generic error.
        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.SectionRead(21, "en-us", TestContext.Current.CancellationToken));

        Assert.Equal("Zendesk Help Center section '21' was not found.", exception.Message);
    }

    [Theory]
    [InlineData(null, "/api/v2/help_center/categories.json")]
    [InlineData("en-us", "/api/v2/help_center/en-us/categories.json")]
    public async Task Categories_Builds_The_Legacy_Path_For_Locale(string? locale, string expectedPath)
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"categories":[]}""");
        var tools = CreateTools(harness);

        await tools.Categories(locale, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, harness.Request.Method);
        Assert.Equal(expectedPath, harness.Request.Path);
    }

    [Fact]
    public async Task Categories_Requests_Offset_Pagination_And_Returns_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"categories":[{"id":360000111222,"name":"Billing","locale":"en-us",
              "url":"https://unit-test.zendesk.com/api/v2/help_center/en-us/categories/360000111222.json"}],
             "count":2,"next_page":null,"previous_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Categories("en-us", 1, 100, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/help_center/en-us/categories.json", request.Path);
        Assert.Contains("&page=1", request.Query);
        Assert.Contains("per_page=100", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.False(result.GetProperty("has_more").GetBoolean());
        var category = result.GetProperty("items")[0];
        Assert.Equal(360000111222, category.GetProperty("id").GetInt64());
        Assert.Equal("Billing", category.GetProperty("name").GetString());
        // Summary rows are allowlisted — fields outside the category shape do not appear.
        Assert.False(category.TryGetProperty("locale", out _));
    }

    [Fact]
    public async Task Categories_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"categories":[]}""");
        var tools = CreateTools(harness);

        await tools.Categories(cancellationToken: TestContext.Current.CancellationToken);

        // Down from the old 100 default — explicit on the wire, never left to Zendesk's server default.
        Assert.Equal("?per_page=30", harness.Request.Query);
    }

    [Fact]
    public async Task Categories_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Categories(detail: "raw", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task CategoryRead_Requests_Locale_Scoped_Category_And_Returns_The_Full_View()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"category":{"id":360000111222,"name":"Billing","locale":"en-us","description":null,
             "url":"https://unit-test.zendesk.com/api/v2/help_center/en-us/categories/360000111222.json"}}
            """);
        var tools = CreateTools(harness);

        var category = await tools.CategoryRead(360000111222, "en-us", TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/help_center/en-us/categories/360000111222.json", harness.Request.Path);
        Assert.Equal(360000111222, category.GetProperty("id").GetInt64());
        Assert.Equal("Billing", category.GetProperty("name").GetString());
        // Full view: the API self-link and null-valued fields are omitted (absent = null/empty).
        Assert.False(category.TryGetProperty("url", out _));
        Assert.False(category.TryGetProperty("description", out _));
    }

    [Fact]
    public async Task CategoryRead_Throws_When_Category_Is_Missing()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        // McpException so the MCP SDK surfaces the message verbatim instead of an opaque generic error.
        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.CategoryRead(31, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("/api/v2/help_center/categories/31.json", harness.Request.Path);
        Assert.Equal("Zendesk Help Center category '31' was not found.", exception.Message);
    }

    [Fact]
    public async Task DeflectionSearch_Requests_Suggestions_And_Returns_Title_And_Link_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"results":[
              {"name":"Reset your password","html_url":"https://acme.zendesk.com/hc/en-us/articles/1"},
              {"name":"Two-factor setup","html_url":"https://acme.zendesk.com/hc/en-us/articles/2"}]}
            """);
        var tools = CreateTools(harness);

        var result = await tools.DeflectionSearch("how do I reset my password",
            TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/help_center/deflection/suggestions.json", request.Path);
        Assert.Contains("query=how", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        var suggestion = result.GetProperty("items")[0];
        Assert.Equal("Reset your password", suggestion.GetProperty("name").GetString());
        Assert.Equal("https://acme.zendesk.com/hc/en-us/articles/1",
            suggestion.GetProperty("html_url").GetString());
    }

    [Fact]
    public async Task DeflectionSearch_Rejects_A_Blank_Query_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        await Assert.ThrowsAsync<McpException>(() =>
            tools.DeflectionSearch("  ", TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }
}