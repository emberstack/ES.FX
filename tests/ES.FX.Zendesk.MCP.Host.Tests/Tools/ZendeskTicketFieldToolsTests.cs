using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskTicketFieldToolsTests
{
    private static (ZendeskTicketFieldTools Tools, Mock<IZendeskTicketFieldsApi> Fields) Create()
    {
        var fields = new Mock<IZendeskTicketFieldsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.TicketFields).Returns(fields.Object);
        return (new ZendeskTicketFieldTools(client.Object), fields);
    }

    [Fact]
    public async Task List_Delegates()
    {
        var expected = new ZendeskTicketFieldsResult { Count = 10 };
        var (tools, fields) = Create();
        fields.Setup(api => api.ListAsync(It.IsAny<int?>(), It.IsAny<string?>(),
            It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.List(TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        // The tool deliberately requests the unpaginated full list (no cursor arguments).
        fields.Verify(api => api.ListAsync(null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskTicketField { Id = 1, Title = "Tier" };
        var (tools, fields) = Create();
        fields.Setup(api => api.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var field = await tools.Read(1, TestContext.Current.CancellationToken);

        Assert.Same(expected, field);
    }
}