using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskOrganizationToolsTests
{
    private static (ZendeskOrganizationTools Tools, Mock<IZendeskOrganizationsApi> Orgs) Create()
    {
        var orgs = new Mock<IZendeskOrganizationsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Organizations).Returns(orgs.Object);
        return (new ZendeskOrganizationTools(client.Object), orgs);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskOrganization { Id = 7, Name = "Acme" };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var org = await tools.Read(7, TestContext.Current.CancellationToken);

        Assert.Same(expected, org);
    }

    [Fact]
    public async Task Tickets_Delegates()
    {
        var expected = new ZendeskTicketsResult { Count = 5 };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.GetTicketsAsync(7, null, 25, It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Tickets(7, null, 25, null, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        orgs.Verify(api => api.GetTicketsAsync(7, null, 25, It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}