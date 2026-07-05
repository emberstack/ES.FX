using System.Net;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskTicketToolsTests
{
    private static (ZendeskTicketTools Tools, Mock<IZendeskTicketsApi> Tickets) Create()
    {
        var tickets = new Mock<IZendeskTicketsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Tickets).Returns(tickets.Object);
        return (new ZendeskTicketTools(client.Object), tickets);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskTicket { Id = 99 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var ticket = await tools.Read(99, TestContext.Current.CancellationToken);

        Assert.Same(expected, ticket);
    }

    [Fact]
    public async Task Search_Passes_Parameters_And_Sideloads_Through()
    {
        var expected = new ZendeskTicketSearchResults { Count = 1 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.SearchAsync("status:open", "created_at", "desc", 2, 50,
            It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Search("status:open", "created_at", "desc", 2, 50, ["users", "groups"],
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.SearchAsync("status:open", "created_at", "desc", 2, 50,
            It.Is<IReadOnlyList<string>>(i => i != null && i.Contains("users") && i.Contains("groups")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Comments_Delegates_With_Pagination_And_BodyFormat()
    {
        var expected = new ZendeskTicketCommentsResult { Count = 3 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetCommentsAsync(99, 1, 25, "plain", It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Comments(99, 1, 25, "plain", null, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetCommentsAsync(99, 1, 25, "plain", null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Comments_Passes_Sideloads_Through()
    {
        var expected = new ZendeskTicketCommentsResult { Count = 3 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetCommentsAsync(99, null, 25, "plain", It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Comments(99, null, 25, "plain", ["users"],
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetCommentsAsync(99, null, 25, "plain",
            It.Is<IReadOnlyList<string>>(i => i != null && i.Count == 1 && i.Contains("users")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Audits_Delegates()
    {
        var expected = new ZendeskTicketAuditsResult { Count = 2 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetAuditsAsync(99, null, 25, It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Audits(99, null, 25, null, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task Audits_Passes_Sideloads_Through()
    {
        var expected = new ZendeskTicketAuditsResult { Count = 2 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetAuditsAsync(99, null, 25, It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Audits(99, null, 25, ["users", "groups", "organizations"],
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetAuditsAsync(99, null, 25,
            It.Is<IReadOnlyList<string>>(i =>
                i != null && i.Contains("users") && i.Contains("groups") && i.Contains("organizations")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Metrics_Delegates()
    {
        var expected = new ZendeskTicketMetric { TicketId = 99, Reopens = 2 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetMetricsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Metrics(99, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task Incidents_Delegates()
    {
        var expected = new ZendeskTicketsResult { Count = 4 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetIncidentsAsync(500, null, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Incidents(500, null, 25, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetIncidentsAsync(500, null, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SideConversations_Delegates()
    {
        var expected = new ZendeskSideConversationsResult { Count = 1 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetSideConversationsAsync(99, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.SideConversations(99, null, null, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task MetricEvents_Delegates_To_The_Incremental_Export()
    {
        var expected = new ZendeskMetricEventsResult { Count = 6, EndOfStream = true };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetMetricEventsAsync(1690000000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.MetricEvents(1690000000, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetMetricEventsAsync(1690000000, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_Delegates_With_Cursor_And_Sideloads()
    {
        var expected = new ZendeskTicketsResult { Count = 7 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.ListAsync(50, "cursor-1", It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.List(50, "cursor-1", ["users"], TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.ListAsync(50, "cursor-1",
            It.Is<IReadOnlyList<string>>(i => i != null && i.Count == 1 && i.Contains("users")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReadMany_Delegates_With_Ids_And_Sideloads()
    {
        var expected = new ZendeskTicketsResult { Count = 2 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetManyAsync(It.IsAny<IReadOnlyList<long>>(), It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.ReadMany([11, 22], ["comment_count"], TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetManyAsync(
            It.Is<IReadOnlyList<long>>(i => i.Count == 2 && i.Contains(11) && i.Contains(22)),
            It.Is<IReadOnlyList<string>>(i => i != null && i.Contains("comment_count")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Count_Delegates()
    {
        var expected = new ZendeskCount { Value = 1234 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Count(TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ReadByExternalId_Delegates()
    {
        var expected = new ZendeskTicketsResult { Count = 1 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetByExternalIdAsync("crm-42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.ReadByExternalId("crm-42", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetByExternalIdAsync("crm-42", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Collaborators_Delegates()
    {
        var expected = new ZendeskUsersResult { Count = 3 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetCollaboratorsAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Collaborators(99, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task CommentsCount_Delegates()
    {
        var expected = new ZendeskCount { Value = 12 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetCommentsCountAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.CommentsCount(99, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetCommentsCountAsync(99, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Incremental_Delegates_With_StartTime_And_Sideloads()
    {
        var expected = new ZendeskIncrementalTicketsResult { EndOfStream = false, AfterCursor = "next" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetIncrementalAsync(1690000000, null, It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Incremental(1690000000, null, ["users"], TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetIncrementalAsync(1690000000, null,
            It.Is<IReadOnlyList<string>>(i => i != null && i.Count == 1 && i.Contains("users")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Incremental_Delegates_With_Cursor()
    {
        var expected = new ZendeskIncrementalTicketsResult { EndOfStream = true };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetIncrementalAsync(null, "cursor-2", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Incremental(null, "cursor-2", null, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetIncrementalAsync(null, "cursor-2", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Read_Surfaces_ZendeskApiException_As_McpException_With_Status_And_Body()
    {
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ZendeskApiException(
                HttpStatusCode.NotFound, "{\"error\":\"RecordNotFound\"}", "request failed"));

        // The MCP SDK discards non-McpException detail; the tool must re-throw an McpException so the agent
        // sees the real status and Zendesk error body (and can distinguish 404 from 403/422) to self-correct.
        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(99, TestContext.Current.CancellationToken));

        Assert.Contains("404", exception.Message);
        Assert.Contains("RecordNotFound", exception.Message);
    }

    [Fact]
    public async Task SearchExport_Delegates_To_The_Search_Api()
    {
        // tickets_search_export is a ticket-area tool (its name says so) that delegates to the unified
        // Search API's cursor export. It lives on ZendeskTicketTools to keep the search area homogeneous.
        var expected = new ZendeskTicketSearchExportResults();
        var search = new Mock<IZendeskSearchApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Search).Returns(search.Object);
        var tools = new ZendeskTicketTools(client.Object);
        search.Setup(api => api.ExportTicketsAsync("status:open", 100, "cursor-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.SearchExport("status:open", 100, "cursor-1",
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        search.Verify(api => api.ExportTicketsAsync("status:open", 100, "cursor-1",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}