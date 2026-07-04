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

        var result = await tools.Comments(99, 1, 25, "plain", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.GetCommentsAsync(99, 1, 25, "plain", It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Audits_Delegates()
    {
        var expected = new ZendeskTicketAuditsResult { Count = 2 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.GetAuditsAsync(99, null, 25, It.IsAny<IReadOnlyList<string>?>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Audits(99, null, 25, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
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
}