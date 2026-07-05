using System.Net;
using System.Text.Json.Nodes;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using ES.FX.NousResearch.HermesAgent.Runs;
using ES.FX.NousResearch.HermesAgent.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.NousResearch.HermesAgent.Tests.Runs;

public class HermesAgentRunsApiTests
{
    private static HermesAgentRunsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8642/") },
            NullLogger<HermesAgentRunsApi>.Instance);

    [Fact]
    public async Task CreateAsync_Posts_Input_And_Parses_202_Acknowledgement()
    {
        var stub = new StubHttpMessageHandler(
            """{ "run_id": "run_0123456789abcdef0123456789abcdef", "status": "started" }""",
            HttpStatusCode.Accepted);
        var api = CreateApi(stub);

        var created = await api.CreateAsync(new HermesAgentRunRequest
        {
            Input = "Summarize the news",
            SessionId = "task-42"
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/runs", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, stub.LastRequest?.Method);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.Equal("Summarize the news", body["input"]!.GetValue<string>()); // string form → JSON string
        Assert.Equal("task-42", body["session_id"]!.GetValue<string>());
        Assert.False(body.ContainsKey("model")); // unset properties are omitted

        Assert.Equal("run_0123456789abcdef0123456789abcdef", created.RunId);
        Assert.Equal("started", created.Status);
    }

    [Fact]
    public async Task CreateAsync_Serializes_Message_List_Input_And_History()
    {
        var stub = new StubHttpMessageHandler("""{ "run_id": "run_1", "status": "started" }""",
            HttpStatusCode.Accepted);
        var api = CreateApi(stub);

        await api.CreateAsync(new HermesAgentRunRequest
        {
            Input = HermesAgentRunInput.FromMessages(
            [
                new HermesAgentRunMessage { Role = "user", Content = "earlier question" },
                new HermesAgentRunMessage { Role = "user", Content = "current question" }
            ]),
            ConversationHistory =
            [
                new HermesAgentRunMessage { Role = "assistant", Content = "earlier answer" }
            ]
        }, cancellationToken: TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        var input = body["input"]!.AsArray();
        Assert.Equal(2, input.Count);
        Assert.Equal("current question", input[1]!["content"]!.GetValue<string>());
        var history = body["conversation_history"]!.AsArray();
        Assert.Equal("assistant", history[0]!["role"]!.GetValue<string>());
        Assert.Equal("earlier answer", history[0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses_Status_Object()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "object": "hermes.run",
              "run_id": "run_1",
              "status": "completed",
              "created_at": 1751700000.123,
              "updated_at": 1751700042.456,
              "session_id": "task-42",
              "model": "hermes-agent",
              "last_event": "run.completed",
              "output": "All done.",
              "usage": { "input_tokens": 5, "output_tokens": 6, "total_tokens": 11 }
            }
            """);
        var api = CreateApi(stub);

        var run = await api.GetByIdAsync("run_1", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/runs/run_1", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal("hermes.run", run.Object);
        Assert.Equal("completed", run.Status);
        Assert.Equal(1751700000.123, run.CreatedAt);
        Assert.Equal("run.completed", run.LastEvent);
        Assert.Equal("All done.", run.Output);
        Assert.Equal(11, run.Usage?.TotalTokens);
        Assert.Null(run.Error);
    }

    [Fact]
    public async Task StopAsync_Posts_To_Stop_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "run_id": "run_1", "status": "stopping" }""");
        var api = CreateApi(stub);

        var stopped = await api.StopAsync("run_1", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/runs/run_1/stop", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, stub.LastRequest?.Method);
        Assert.Equal("stopping", stopped.Status);
    }

    [Fact]
    public async Task ResolveApprovalAsync_Posts_Choice_And_Parses_Result()
    {
        var stub = new StubHttpMessageHandler(
            """{ "object": "hermes.run.approval_response", "run_id": "run_1", "choice": "once", "resolved": 1 }""");
        var api = CreateApi(stub);

        var result = await api.ResolveApprovalAsync("run_1",
            new HermesAgentRunApprovalRequest { Choice = "once", All = true },
            TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/runs/run_1/approval", stub.LastRequest?.RequestUri?.ToString());
        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.Equal("once", body["choice"]!.GetValue<string>());
        Assert.True(body["all"]!.GetValue<bool>());
        Assert.False(body.ContainsKey("resolve_all")); // unset properties are omitted

        Assert.Equal("hermes.run.approval_response", result.Object);
        Assert.Equal("once", result.Choice);
        Assert.Equal(1, result.Resolved);
    }

    [Fact]
    public async Task StreamEventsAsync_Maps_Data_Only_Events_By_Payload_Event_Key()
    {
        // The run feed is data-only SSE (no `event:` lines): the payload's "event" key discriminates.
        const string sse =
            """
            data: {"event":"message.delta","run_id":"run_1","timestamp":1751700001.5,"delta":"Working on"}

            data: {"event":"tool.started","run_id":"run_1","timestamp":1751700002.0,"tool":"terminal","preview":"ls -la"}

            data: {"event":"tool.completed","run_id":"run_1","timestamp":1751700003.0,"tool":"terminal","duration":1.234,"error":false}

            data: {"event":"reasoning.available","run_id":"run_1","timestamp":1751700003.5,"text":"I should list files first."}

            data: {"event":"approval.request","run_id":"run_1","timestamp":1751700004.0,"command":"rm -rf [redacted]","description":"Delete build artifacts","choices":["once","session","always","deny"]}

            data: {"event":"approval.responded","run_id":"run_1","timestamp":1751700005.0,"choice":"once","resolved":1}

            data: {"event":"run.completed","run_id":"run_1","timestamp":1751700006.0,"output":"All done.","usage":{"input_tokens":5,"output_tokens":6,"total_tokens":11}}


            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        var events = new List<HermesAgentRunEvent>();
        await foreach (var runEvent in api.StreamEventsAsync("run_1", TestContext.Current.CancellationToken))
            events.Add(runEvent);

        Assert.Equal("http://localhost:8642/v1/runs/run_1/events", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Get, stub.LastRequest?.Method);
        Assert.Equal(7, events.Count);

        var delta = Assert.IsType<HermesAgentRunMessageDeltaEvent>(events[0]);
        Assert.Equal("message.delta", delta.Event);
        Assert.Equal("run_1", delta.RunId);
        Assert.Equal(1751700001.5, delta.Timestamp);
        Assert.Equal("Working on", delta.Delta);

        var toolStarted = Assert.IsType<HermesAgentRunToolStartedEvent>(events[1]);
        Assert.Equal("terminal", toolStarted.Tool);
        Assert.Equal("ls -la", toolStarted.Preview);

        var toolCompleted = Assert.IsType<HermesAgentRunToolCompletedEvent>(events[2]);
        Assert.Equal(1.234, toolCompleted.Duration);
        Assert.False(toolCompleted.Error);

        var reasoning = Assert.IsType<HermesAgentRunReasoningAvailableEvent>(events[3]);
        Assert.Equal("I should list files first.", reasoning.Text);

        var approval = Assert.IsType<HermesAgentRunApprovalRequestEvent>(events[4]);
        Assert.Equal("rm -rf [redacted]", approval.Command);
        Assert.Equal("Delete build artifacts", approval.Description);
        Assert.Equal(new[] { "once", "session", "always", "deny" }, approval.Choices);

        var responded = Assert.IsType<HermesAgentRunApprovalRespondedEvent>(events[5]);
        Assert.Equal("once", responded.Choice);
        Assert.Equal(1, responded.Resolved);

        var completed = Assert.IsType<HermesAgentRunCompletedEvent>(events[6]);
        Assert.Equal("All done.", completed.Output);
        Assert.Equal(11, completed.Usage?.TotalTokens);
    }

    [Fact]
    public async Task StreamEventsAsync_Maps_Terminal_Failure_And_Cancellation_Events()
    {
        const string sse =
            """
            data: {"event":"run.failed","run_id":"run_1","timestamp":1751700001.0,"error":"agent exploded"}

            data: {"event":"run.cancelled","run_id":"run_1","timestamp":1751700002.0}


            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        var events = new List<HermesAgentRunEvent>();
        await foreach (var runEvent in api.StreamEventsAsync("run_1", TestContext.Current.CancellationToken))
            events.Add(runEvent);

        var failed = Assert.IsType<HermesAgentRunFailedEvent>(events[0]);
        Assert.Equal("agent exploded", failed.Error);
        var cancelled = Assert.IsType<HermesAgentRunCancelledEvent>(events[1]);
        Assert.Equal("run.cancelled", cancelled.Event);
    }

    [Fact]
    public async Task StreamEventsAsync_Unknown_Event_Names_And_Non_Json_Payloads_Never_Throw()
    {
        const string sse =
            """
            data: {"event":"telepathy.engaged","run_id":"run_1","timestamp":1751700001.0,"brainwaves":9000}

            data: not json at all

            data: {"no_event_key":true}


            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        var events = new List<HermesAgentRunEvent>();
        await foreach (var runEvent in api.StreamEventsAsync("run_1", TestContext.Current.CancellationToken))
            events.Add(runEvent);

        Assert.Equal(3, events.Count);

        var unknownName = Assert.IsType<HermesAgentRunUnknownEvent>(events[0]);
        Assert.Equal("telepathy.engaged", unknownName.EventType); // payload event key wins
        Assert.Contains("brainwaves", unknownName.Data);

        var notJson = Assert.IsType<HermesAgentRunUnknownEvent>(events[1]);
        Assert.Equal("message", notJson.EventType); // falls back to the SSE default event name
        Assert.Equal("not json at all", notJson.Data);

        var noEventKey = Assert.IsType<HermesAgentRunUnknownEvent>(events[2]);
        Assert.Equal("message", noEventKey.EventType);
    }

    [Fact]
    public async Task StreamEventsAsync_Non_Success_Response_Throws_Api_Exception_Before_Any_Event()
    {
        // The response guard also runs on the GET SSE path — an error body must throw on the first
        // MoveNextAsync instead of being parsed as unknown events.
        var stub = new StubHttpMessageHandler(
            """{ "error": { "message": "Invalid API key", "type": "authentication_error" } }""",
            HttpStatusCode.Unauthorized);
        var api = CreateApi(stub);

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(async () =>
        {
            await foreach (var _ in api.StreamEventsAsync("run_1", TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal("Invalid API key", exception.Error?.Message);
        Assert.Equal("authentication_error", exception.Error?.Type);
    }
}
