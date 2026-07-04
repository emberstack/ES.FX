using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Tools;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskJobStatusToolsTests
{
    private static (ZendeskJobStatusTools Tools, Mock<IZendeskJobStatusesApi> JobStatuses) Create()
    {
        var jobStatuses = new Mock<IZendeskJobStatusesApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.JobStatuses).Returns(jobStatuses.Object);
        return (new ZendeskJobStatusTools(client.Object), jobStatuses);
    }

    [Fact]
    public async Task List_Delegates()
    {
        var expected = new ZendeskJobStatusesResult();
        var (tools, jobStatuses) = Create();
        jobStatuses.Setup(api => api.ListAsync(100, "cursor-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.List(100, "cursor-1", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        jobStatuses.Verify(api => api.ListAsync(100, "cursor-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Read_Delegates_To_GetById()
    {
        var expected = new ZendeskJobStatus { Id = "job-abc" };
        var (tools, jobStatuses) = Create();
        jobStatuses.Setup(api => api.GetByIdAsync("job-abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var jobStatus = await tools.Read("job-abc", TestContext.Current.CancellationToken);

        Assert.Same(expected, jobStatus);
    }

    [Fact]
    public async Task ReadMany_Delegates_To_GetMany()
    {
        var expected = new ZendeskJobStatusesResult();
        var ids = new[] { "job-a", "job-b" };
        var (tools, jobStatuses) = Create();
        jobStatuses.Setup(api => api.GetManyAsync(ids, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.ReadMany(ids, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        jobStatuses.Verify(api => api.GetManyAsync(ids, It.IsAny<CancellationToken>()), Times.Once);
    }
}
