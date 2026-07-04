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

    [Fact]
    public async Task List_Delegates()
    {
        var expected = new ZendeskOrganizationsResult { Count = 9 };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.ListAsync(50, "cursor-1", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.List(50, "cursor-1", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        orgs.Verify(api => api.ListAsync(50, "cursor-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Count_Delegates()
    {
        var expected = new ZendeskCount { Value = 321 };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Count(TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ReadMany_Delegates()
    {
        var ids = new long[] { 7, 8, 9 };
        var expected = new ZendeskOrganizationsResult { Count = 3 };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.GetManyAsync(It.Is<IReadOnlyList<long>>(value => ReferenceEquals(value, ids)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.ReadMany(ids, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        orgs.Verify(api => api.GetManyAsync(It.Is<IReadOnlyList<long>>(value => ReferenceEquals(value, ids)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_Delegates_With_Name()
    {
        var expected = new ZendeskOrganizationsResult { Count = 1 };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.SearchAsync("Acme", null, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Search("Acme", null, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        orgs.Verify(api => api.SearchAsync("Acme", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_Delegates_With_ExternalId()
    {
        var expected = new ZendeskOrganizationsResult { Count = 1 };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.SearchAsync(null, "ext-42", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Search(null, "ext-42", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        orgs.Verify(api => api.SearchAsync(null, "ext-42", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Autocomplete_Delegates()
    {
        var expected = new ZendeskOrganizationsResult { Count = 2 };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.AutocompleteAsync("Acm", 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Autocomplete("Acm", 1, 20, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        orgs.Verify(api => api.AutocompleteAsync("Acm", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Users_Delegates_And_Passes_Include_Through()
    {
        var include = new[] { "identities" };
        var expected = new ZendeskUsersResult { Count = 4 };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.GetUsersAsync(7, 1, 50,
                It.Is<IReadOnlyList<string>?>(value => ReferenceEquals(value, include)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Users(7, 1, 50, include, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        orgs.Verify(api => api.GetUsersAsync(7, 1, 50,
            It.Is<IReadOnlyList<string>?>(value => ReferenceEquals(value, include)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Memberships_Delegates()
    {
        var expected = new ZendeskOrganizationMembershipsResult { Count = 5 };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.GetMembershipsAsync(7, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await tools.Memberships(7, null, 100, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        orgs.Verify(api => api.GetMembershipsAsync(7, null, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MergeStatus_Delegates()
    {
        var expected = new ZendeskOrganizationMerge { Id = "merge-1", Status = "complete" };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.GetMergeAsync("merge-1", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.MergeStatus("merge-1", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task Tags_Delegates()
    {
        var expected = new ZendeskTagNamesResult { Tags = ["vip"] };
        var (tools, orgs) = Create();
        orgs.Setup(api => api.GetTagsAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await tools.Tags(7, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        orgs.Verify(api => api.GetTagsAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }
}