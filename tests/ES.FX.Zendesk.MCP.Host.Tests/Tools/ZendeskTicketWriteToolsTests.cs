using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskTicketWriteToolsTests
{
    private static (ZendeskTicketWriteTools Tools, Mock<IZendeskTicketsApi> Tickets) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var tickets = new Mock<IZendeskTicketsApi>();
        var client = new Mock<IZendeskClient>();
        client.SetupGet(c => c.Tickets).Returns(tickets.Object);
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(a => a.EffectiveMode).Returns(mode);
        return (new ZendeskTicketWriteTools(client.Object, executionMode.Object), tickets);
    }

    [Fact]
    public async Task Create_Delegates()
    {
        var write = new ZendeskTicketWrite { Subject = "Broken widget" };
        var expected = new ZendeskTicket { Id = 1 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.CreateAsync(write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.CreateAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMany_Delegates()
    {
        var writes = new[] { new ZendeskTicketWrite { Subject = "A" }, new ZendeskTicketWrite { Subject = "B" } };
        var expected = new ZendeskJobStatus { Id = "job1" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.CreateManyAsync(writes, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.CreateMany(writes, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.CreateManyAsync(writes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Delegates()
    {
        var write = new ZendeskTicketWrite { Status = "solved" };
        var expected = new ZendeskTicketUpdateResult { Ticket = new ZendeskTicket { Id = 42 } };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.UpdateAsync(42, write, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.UpdateAsync(42, write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMany_Delegates()
    {
        var ids = new long[] { 1, 2, 3 };
        var change = new ZendeskTicketWrite { Priority = "high" };
        var expected = new ZendeskJobStatus { Id = "job2" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.UpdateManyAsync(ids, change, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.UpdateMany(ids, change, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.UpdateManyAsync(ids, change, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateManyBatch_Delegates()
    {
        var writes = new[] { new ZendeskTicketWrite { Id = 1, Status = "open" } };
        var expected = new ZendeskJobStatus { Id = "job3" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.UpdateManyAsync(writes, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.UpdateManyBatch(writes, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.UpdateManyAsync(writes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_Delegates_And_Acknowledges()
    {
        var (tools, tickets) = Create();

        var result = await tools.Delete(7, TestContext.Current.CancellationToken);

        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("soft-delete ticket 7", acknowledgement.Description);
        tickets.Verify(api => api.DeleteAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMany_Delegates()
    {
        var ids = new long[] { 7, 8 };
        var expected = new ZendeskJobStatus { Id = "job4" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.DeleteManyAsync(ids, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.DeleteMany(ids, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.DeleteManyAsync(ids, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Merge_Delegates()
    {
        var sources = new long[] { 5, 6 };
        var expected = new ZendeskJobStatus { Id = "job5" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.MergeAsync(9, sources, "target note", "source note", true, false,
            It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Merge(9, sources, "target note", "source note", true, false,
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.MergeAsync(9, sources, "target note", "source note", true, false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkSpam_Delegates_And_Acknowledges()
    {
        var (tools, tickets) = Create();

        var result = await tools.MarkSpam(11, TestContext.Current.CancellationToken);

        Assert.IsType<ZendeskWriteAcknowledgement>(result);
        tickets.Verify(api => api.MarkAsSpamAsync(11, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkSpamMany_Delegates()
    {
        var ids = new long[] { 11, 12 };
        var expected = new ZendeskJobStatus { Id = "job6" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.MarkManyAsSpamAsync(ids, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.MarkSpamMany(ids, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.MarkManyAsSpamAsync(ids, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Restore_Delegates_And_Acknowledges()
    {
        var (tools, tickets) = Create();

        var result = await tools.Restore(13, TestContext.Current.CancellationToken);

        Assert.IsType<ZendeskWriteAcknowledgement>(result);
        tickets.Verify(api => api.RestoreDeletedAsync(13, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreMany_Delegates_And_Acknowledges()
    {
        var ids = new long[] { 13, 14 };
        var (tools, tickets) = Create();

        var result = await tools.RestoreMany(ids, TestContext.Current.CancellationToken);

        Assert.IsType<ZendeskWriteAcknowledgement>(result);
        tickets.Verify(api => api.RestoreManyDeletedAsync(ids, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePermanently_Delegates()
    {
        var expected = new ZendeskJobStatus { Id = "job7" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.DeletePermanentlyAsync(15, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.DeletePermanently(15, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.DeletePermanentlyAsync(15, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePermanentlyMany_Delegates()
    {
        var ids = new long[] { 15, 16 };
        var expected = new ZendeskJobStatus { Id = "job8" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.DeleteManyPermanentlyAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.DeletePermanentlyMany(ids, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.DeleteManyPermanentlyAsync(ids, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TagsSet_Delegates()
    {
        var tags = new[] { "vip", "billing" };
        var expected = new ZendeskTagNamesResult { Tags = tags };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.SetTagsAsync(21, tags, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.TagsSet(21, tags, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.SetTagsAsync(21, tags, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TagsAdd_Delegates_With_UpdatedStamp()
    {
        var tags = new[] { "urgent" };
        var stamp = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var expected = new ZendeskTagNamesResult { Tags = tags };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.AddTagsAsync(21, tags, stamp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.TagsAdd(21, tags, stamp, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.AddTagsAsync(21, tags, stamp, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TagsRemove_Delegates_With_UpdatedStamp()
    {
        var tags = new[] { "stale" };
        var stamp = new DateTimeOffset(2026, 7, 2, 11, 0, 0, TimeSpan.Zero);
        var expected = new ZendeskTagNamesResult();
        var (tools, tickets) = Create();
        tickets.Setup(api => api.RemoveTagsAsync(21, tags, stamp, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.TagsRemove(21, tags, stamp, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.RemoveTagsAsync(21, tags, stamp, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommentMakePrivate_Delegates_And_Acknowledges()
    {
        var (tools, tickets) = Create();

        var result = await tools.CommentMakePrivate(31, 32, TestContext.Current.CancellationToken);

        Assert.IsType<ZendeskWriteAcknowledgement>(result);
        tickets.Verify(api => api.MakeCommentPrivateAsync(31, 32, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommentAttachmentRedact_Delegates()
    {
        var expected = new ZendeskAttachment { Id = 33, FileName = "redacted.txt" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.RedactCommentAttachmentAsync(31, 32, 33, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.CommentAttachmentRedact(31, 32, 33, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.RedactCommentAttachmentAsync(31, 32, 33, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Import_Delegates_With_ArchiveImmediately()
    {
        var import = new ZendeskTicketImport { Subject = "Legacy ticket", Status = "closed" };
        var expected = new ZendeskTicket { Id = 51 };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.ImportAsync(import, true, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Import(import, true, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.ImportAsync(import, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportMany_Delegates_With_ArchiveImmediately()
    {
        var imports = new[] { new ZendeskTicketImport { Subject = "Legacy A" } };
        var expected = new ZendeskJobStatus { Id = "job9" };
        var (tools, tickets) = Create();
        tickets.Setup(api => api.ImportManyAsync(imports, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.ImportMany(imports, true, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        tickets.Verify(api => api.ImportManyAsync(imports, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Zendesk()
    {
        var write = new ZendeskTicketWrite { Status = "solved" };
        var (tools, tickets) = Create(McpExecutionMode.DryRun);

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("update ticket 42", dryRun.Description);
        Assert.NotNull(dryRun.Request);
        tickets.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Zendesk()
    {
        var (tools, tickets) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(7, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        tickets.VerifyNoOtherCalls();
    }
}
