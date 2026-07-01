using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskFormToolsTests
{
    private static (ZendeskFormTools Tools, Mock<IZendeskFormsApi> Forms) Create()
    {
        var forms = new Mock<IZendeskFormsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Forms).Returns(forms.Object);
        return (new ZendeskFormTools(client.Object), forms);
    }

    [Fact]
    public async Task Search_Delegates_To_List()
    {
        var expected = new ZendeskTicketFormsResult { Count = 2 };
        var (tools, forms) = Create();
        forms.Setup(api => api.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Search(TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        forms.Verify(api => api.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskTicketForm { Id = 7 };
        var (tools, forms) = Create();
        forms.Setup(api => api.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var form = await tools.Read(7, TestContext.Current.CancellationToken);

        Assert.Same(expected, form);
        forms.Verify(api => api.GetByIdAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }
}