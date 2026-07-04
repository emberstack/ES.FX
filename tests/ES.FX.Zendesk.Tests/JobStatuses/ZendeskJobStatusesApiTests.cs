using ES.FX.Zendesk.JobStatuses;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.JobStatuses;

public class ZendeskJobStatusesApiTests
{
    private static ZendeskJobStatusesApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskJobStatusesApi>.Instance);

    [Fact]
    public async Task ListAsync_Uses_Cursor_Params_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "job_statuses": [ { "id": "abc123", "status": "completed", "total": 2, "progress": 2 } ], "meta": { "has_more": false } }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(50, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.JobStatuses);
        Assert.Equal("completed", result.JobStatuses[0].Status);
        Assert.Equal("/api/v2/job_statuses.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("page[size]=50", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetByIdAsync_Escapes_The_Opaque_Id_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "job_status": { "id": "abc/123", "status": "working", "total": 10, "progress": 4, "results": [ { "index": 0 } ] } }""");
        var api = CreateApi(stub);

        var job = await api.GetByIdAsync("abc/123", TestContext.Current.CancellationToken);

        Assert.Equal("working", job.Status);
        Assert.Equal(4, job.Progress);
        Assert.NotNull(job.Results);
        // The opaque id is escaped so it cannot inject extra path segments.
        Assert.Contains("job_statuses/abc%2F123.json", stub.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetManyAsync_Joins_Ids_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "job_statuses": [ { "id": "a", "status": "queued" }, { "id": "b", "status": "completed" } ] }""");
        var api = CreateApi(stub);

        var result = await api.GetManyAsync(["a", "b"], TestContext.Current.CancellationToken);

        Assert.Equal(2, result.JobStatuses.Count);
        Assert.Equal("/api/v2/job_statuses/show_many.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ids=a%2Cb", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetManyAsync_Empty_List_Returns_Without_A_Call()
    {
        var counting = new CountingHandler(_ => new HttpResponseMessage());
        var api = CreateApi(counting);

        var result = await api.GetManyAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(result.JobStatuses);
        Assert.Equal(0, counting.Calls);
    }

    [Fact]
    public async Task GetManyAsync_Rejects_More_Than_100_Ids()
    {
        var api = CreateApi(new StubHttpMessageHandler("{}"));
        var ids = Enumerable.Range(0, 101).Select(i => $"job-{i}").ToList();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            api.GetManyAsync(ids, TestContext.Current.CancellationToken));
    }
}