using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskAttachmentToolsTests
{
    [Fact]
    public async Task Read_Delegates_To_GetContent()
    {
        var attachments = new Mock<IZendeskAttachmentsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Attachments).Returns(attachments.Object);
        var tools = new ZendeskAttachmentTools(client.Object);

        var expected = new ZendeskAttachmentContent { Id = 88, FileName = "log.txt", Encoding = "utf-8" };
        attachments.Setup(api => api.GetContentAsync(88, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Read(88, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        // The library default is unlimited — the TOOL must cap at 1 MiB to keep agent responses bounded.
        attachments.Verify(api => api.GetContentAsync(88, 1024 * 1024, It.IsAny<CancellationToken>()), Times.Once);
    }
}