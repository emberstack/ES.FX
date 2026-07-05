using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using ES.FX.NousResearch.HermesAgent.Jobs;
using ES.FX.NousResearch.HermesAgent.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.NousResearch.HermesAgent.Tests.Jobs;

public class HermesAgentJobsApiTests
{
    /// <summary>The full stored job shape from the jobs wire spec (create response).</summary>
    private const string StoredJobJson =
        """
        {
          "job": {
            "id": "aabbccddeeff",
            "name": "test-job",
            "prompt": "do something",
            "skills": ["research"],
            "skill": "research",
            "model": null,
            "provider": null,
            "provider_snapshot": "openrouter",
            "model_snapshot": "hermes-4",
            "base_url": null,
            "script": null,
            "no_agent": false,
            "context_from": null,
            "schedule": { "kind": "cron", "expr": "*/5 * * * *", "display": "*/5 * * * *" },
            "schedule_display": "*/5 * * * *",
            "repeat": { "times": null, "completed": 0 },
            "enabled": true,
            "state": "scheduled",
            "paused_at": null,
            "paused_reason": null,
            "created_at": "2026-07-05T09:00:00+02:00",
            "next_run_at": "2026-07-05T09:05:00+02:00",
            "last_run_at": null,
            "last_status": null,
            "last_error": null,
            "last_delivery_error": null,
            "deliver": "local",
            "origin": { "platform": "api_server", "chat_id": "api", "user_agent": "ES.FX.NousResearch.HermesAgent/1.0.0" },
            "enabled_toolsets": null,
            "workdir": null
          }
        }
        """;

    private static HermesAgentJobsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8642/") },
            NullLogger<HermesAgentJobsApi>.Instance);

    [Fact]
    public async Task ListAsync_Unwraps_Jobs_Envelope()
    {
        var stub = new StubHttpMessageHandler(
            """{ "jobs": [ { "id": "aabbccddeeff", "name": "test-job", "enabled": true, "state": "scheduled" } ] }""");
        var api = CreateApi(stub);

        var jobs = await api.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/jobs", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Get, stub.LastRequest?.Method);
        var job = Assert.Single(jobs);
        Assert.Equal("aabbccddeeff", job.Id);
        Assert.Equal("scheduled", job.State);
    }

    [Fact]
    public async Task CreateAsync_Posts_Flat_Write_Body_And_Parses_Stored_Job()
    {
        var stub = new StubHttpMessageHandler(StoredJobJson);
        var api = CreateApi(stub);

        var job = await api.CreateAsync(new HermesAgentJobWrite
        {
            Name = "test-job",
            Schedule = "*/5 * * * *",
            Prompt = "do something"
        }, TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/jobs", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, stub.LastRequest?.Method);

        // The jobs surface takes flat bodies (no request envelope); the schedule is written as a STRING and
        // unset fields are omitted.
        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.Equal("test-job", body["name"]!.GetValue<string>());
        Assert.Equal("*/5 * * * *", body["schedule"]!.GetValue<string>());
        Assert.Equal("do something", body["prompt"]!.GetValue<string>());
        Assert.False(body.ContainsKey("deliver"));
        Assert.False(body.ContainsKey("skills"));
        Assert.False(body.ContainsKey("repeat"));

        Assert.Equal("aabbccddeeff", job.Id);
        Assert.Equal("test-job", job.Name);
        Assert.Equal("cron", job.Schedule?.Kind); // stored parsed, not the raw string
        Assert.Equal("*/5 * * * *", job.Schedule?.Expr);
        Assert.Equal("*/5 * * * *", job.ScheduleDisplay);
        Assert.Null(job.Repeat?.Times);
        Assert.Equal(0, job.Repeat?.Completed);
        Assert.True(job.Enabled);
        Assert.Equal("scheduled", job.State);
        Assert.Equal("local", job.Deliver);
        Assert.Equal("research", Assert.Single(job.Skills));
        Assert.Equal("api_server", job.Origin?.Platform);
        Assert.Equal(new DateTimeOffset(2026, 7, 5, 9, 0, 0, TimeSpan.FromHours(2)), job.CreatedAt);
        Assert.Null(job.LastRunAt);
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Unwraps()
    {
        var stub = new StubHttpMessageHandler(StoredJobJson);
        var api = CreateApi(stub);

        var job = await api.GetByIdAsync("aabbccddeeff", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/jobs/aabbccddeeff", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal("aabbccddeeff", job.Id);
    }

    [Fact]
    public async Task UpdateAsync_Patches_Only_Set_Fields()
    {
        var stub = new StubHttpMessageHandler(StoredJobJson);
        var api = CreateApi(stub);

        await api.UpdateAsync("aabbccddeeff", new HermesAgentJobWrite
        {
            Name = "updated-name",
            Schedule = "0 * * * *"
        }, TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/jobs/aabbccddeeff", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Patch, stub.LastRequest?.Method);

        // Omit-null write serialization: a PATCH carries only the fields the caller set (the server applies a
        // shallow merge, so an accidental null here would overwrite stored state).
        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.Equal(2, body.Count);
        Assert.Equal("updated-name", body["name"]!.GetValue<string>());
        Assert.Equal("0 * * * *", body["schedule"]!.GetValue<string>());
    }

    [Fact]
    public async Task DeleteAsync_Sends_Delete_And_Accepts_Ok_Acknowledgement()
    {
        var stub = new StubHttpMessageHandler("""{ "ok": true }""");
        var api = CreateApi(stub);

        await api.DeleteAsync("aabbccddeeff", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/jobs/aabbccddeeff", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Delete, stub.LastRequest?.Method);
    }

    [Fact]
    public async Task PauseAsync_Posts_Bodyless_Lifecycle_Request_And_Unwraps()
    {
        var stub = new StubHttpMessageHandler(StoredJobJson);
        var api = CreateApi(stub);

        var job = await api.PauseAsync("aabbccddeeff", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/jobs/aabbccddeeff/pause", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, stub.LastRequest?.Method);
        Assert.Null(stub.LastRequestBody); // lifecycle endpoints take no body
        Assert.Equal("aabbccddeeff", job.Id);
    }

    [Fact]
    public async Task ResumeAsync_Posts_To_Resume()
    {
        var stub = new StubHttpMessageHandler(StoredJobJson);
        var api = CreateApi(stub);

        await api.ResumeAsync("aabbccddeeff", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/jobs/aabbccddeeff/resume", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task TriggerAsync_Posts_To_Run_And_Returns_The_Rearmed_Job()
    {
        var stub = new StubHttpMessageHandler(StoredJobJson);
        var api = CreateApi(stub);

        var job = await api.TriggerAsync("aabbccddeeff", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/jobs/aabbccddeeff/run", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, stub.LastRequest?.Method);
        Assert.Equal("aabbccddeeff", job.Id); // the response is the re-armed job, not a run result
    }

    [Fact]
    public async Task Flat_Error_Envelope_Surfaces_As_Api_Exception_With_Message_Only_Error()
    {
        var stub = new StubHttpMessageHandler("""{ "error": "Job not found" }""", HttpStatusCode.NotFound);
        var api = CreateApi(stub);

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(() =>
            api.GetByIdAsync("aabbccddeeff", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal("Job not found", exception.Error?.Message);
        Assert.Null(exception.Error?.Type); // the flat jobs shape carries only the message
        Assert.Contains("Job not found", exception.ResponseBody);
    }

    [Fact]
    public async Task CreateAsync_Writes_Repeat_As_A_Bare_Json_Number()
    {
        // The wire shape is a bare integer (`"repeat": 3`), NOT the stored `{times, completed}` object — a
        // "helpful" serialization change wrapping it would be rejected (create) or corrupt state (update).
        var stub = new StubHttpMessageHandler(StoredJobJson);
        var api = CreateApi(stub);

        await api.CreateAsync(new HermesAgentJobWrite
        {
            Name = "test-job",
            Schedule = "*/5 * * * *",
            Prompt = "do something",
            Repeat = 3
        }, TestContext.Current.CancellationToken);

        var repeat = JsonNode.Parse(stub.LastRequestBody!)!.AsObject()["repeat"]!;
        Assert.Equal(JsonValueKind.Number, repeat.GetValueKind());
        Assert.Equal(3, repeat.GetValue<int>());
    }

    [Fact]
    public async Task UpdateAsync_Writes_Repeat_As_A_Bare_Json_Number()
    {
        // Same bare-number shape on PATCH — this is the documented shape-corrupting footgun, so the client
        // must send exactly what the caller set, without reshaping it.
        var stub = new StubHttpMessageHandler(StoredJobJson);
        var api = CreateApi(stub);

        await api.UpdateAsync("aabbccddeeff", new HermesAgentJobWrite { Repeat = 3 },
            TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        var (key, repeat) = Assert.Single(body); // Repeat is the only set field
        Assert.Equal("repeat", key);
        Assert.Equal(JsonValueKind.Number, repeat!.GetValueKind());
        Assert.Equal(3, repeat.GetValue<int>());
    }

    [Fact]
    public async Task CreateAsync_Throws_On_An_Empty_Envelope()
    {
        // A success body without the `job` member must surface as a clear operation-specific exception, not a
        // NullReferenceException or a null return from a non-nullable API.
        var stub = new StubHttpMessageHandler("{}");
        var api = CreateApi(stub);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.CreateAsync(new HermesAgentJobWrite { Name = "test-job", Schedule = "in 5m", Prompt = "p" },
                TestContext.Current.CancellationToken));

        Assert.Contains("no created job", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_Throws_On_An_Empty_Envelope()
    {
        var stub = new StubHttpMessageHandler("""{ "job": null }""");
        var api = CreateApi(stub);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.UpdateAsync("aabbccddeeff", new HermesAgentJobWrite { Name = "renamed" },
                TestContext.Current.CancellationToken));

        Assert.Contains("no updated job", exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_Escapes_The_Job_Id_In_The_Request_Path()
    {
        // The server only accepts 12-hex ids, but a malformed caller-supplied id must reach it VERBATIM (and
        // fail its validation) instead of mutating the request path.
        var stub = new StubHttpMessageHandler(StoredJobJson);
        var api = CreateApi(stub);

        await api.GetByIdAsync("abc/12 3#x", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/jobs/abc%2F12%203%23x",
            stub.LastRequest?.RequestUri?.AbsoluteUri);
    }
}
