using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskCustomStatusToolsTests
{
    private static (ZendeskCustomStatusTools Tools, Mock<IZendeskCustomStatusesApi> CustomStatuses) Create()
    {
        var customStatuses = new Mock<IZendeskCustomStatusesApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.CustomStatuses).Returns(customStatuses.Object);
        return (new ZendeskCustomStatusTools(client.Object), customStatuses);
    }

    [Fact]
    public async Task List_Delegates()
    {
        var expected = new ZendeskCustomStatusesResult();
        var (tools, customStatuses) = Create();
        customStatuses.Setup(api => api.ListAsync(true, false, "open,pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.List(true, false, "open,pending", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        customStatuses.Verify(api => api.ListAsync(true, false, "open,pending", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskCustomStatus { Id = 31 };
        var (tools, customStatuses) = Create();
        customStatuses.Setup(api => api.GetByIdAsync(31, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var status = await tools.Read(31, TestContext.Current.CancellationToken);

        Assert.Same(expected, status);
    }
}
