using System.Net;
using System.Text.Json.Nodes;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using ES.FX.NousResearch.HermesAgent.Sessions;
using ES.FX.NousResearch.HermesAgent.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.NousResearch.HermesAgent.Tests.Sessions;

public class HermesAgentSessionsApiTests
{
    private const string SessionEnvelopeJson =
        """
        {
          "object": "hermes.session",
          "session": {
            "id": "api_1710000000_a1b2c3d4",
            "source": "api_server",
            "user_id": null,
            "model": "test-model",
            "title": "Mobile chat",
            "started_at": 1710000000.1,
            "ended_at": null,
            "end_reason": null,
            "message_count": 0,
            "tool_call_count": 0,
            "input_tokens": 0,
            "output_tokens": 0,
            "cache_read_tokens": 0,
            "cache_write_tokens": 0,
            "reasoning_tokens": 0,
            "estimated_cost_usd": null,
            "actual_cost_usd": null,
            "api_call_count": 0,
            "parent_session_id": null,
            "has_system_prompt": false,
            "has_model_config": false
          }
        }
        """;

    private static HermesAgentSessionsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8642/") },
            NullLogger<HermesAgentSessionsApi>.Instance);

    [Fact]
    public async Task ListAsync_Builds_Query_String_And_Parses_List_Envelope()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "object": "list",
              "data": [
                {
                  "id": "api_1710000000_a1b2c3d4",
                  "source": "api_server",
                  "title": "Mobile chat",
                  "started_at": 1710000000.1,
                  "message_count": 2,
                  "last_active": 1710000042.5,
                  "preview": "hello from phone",
                  "has_system_prompt": false,
                  "has_model_config": false
                }
              ],
              "limit": 10,
              "offset": 0,
              "has_more": false
            }
            """);
        var api = CreateApi(stub);

        var result = await api.ListAsync(new HermesAgentSessionsQuery
        {
            Limit = 10,
            Offset = 0,
            Source = "api_server",
            IncludeChildren = true
        }, TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/sessions?limit=10&offset=0&source=api_server&include_children=true",
            stub.LastRequest?.RequestUri?.ToString());

        Assert.Equal("list", result.Object);
        var session = Assert.Single(result.Data);
        Assert.Equal("api_1710000000_a1b2c3d4", session.Id);
        Assert.Equal(2, session.MessageCount);
        Assert.Equal(1710000042.5, session.LastActive);
        Assert.Equal("hello from phone", session.Preview);
        Assert.Equal(10, result.Limit);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task ListAsync_Without_Query_Requests_Bare_Path()
    {
        var stub = new StubHttpMessageHandler(
            """{ "object": "list", "data": [], "limit": 50, "offset": 0, "has_more": false }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/sessions", stub.LastRequest?.RequestUri?.ToString());
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task CreateAsync_Defaults_To_Empty_Object_Body_And_Unwraps_Created_Session()
    {
        var stub = new StubHttpMessageHandler(SessionEnvelopeJson, HttpStatusCode.Created);
        var api = CreateApi(stub);

        var session = await api.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/sessions", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, stub.LastRequest?.Method);
        // QUIRK under test: the server requires a JSON OBJECT body even for all-default creates.
        Assert.Equal("{}", stub.LastRequestBody);

        Assert.Equal("api_1710000000_a1b2c3d4", session.Id);
        Assert.Equal("api_server", session.Source);
        Assert.Equal("Mobile chat", session.Title);
        Assert.Equal(1710000000.1, session.StartedAt);
        Assert.False(session.HasSystemPrompt);
    }

    [Fact]
    public async Task CreateAsync_Serializes_Set_Fields_Only()
    {
        var stub = new StubHttpMessageHandler(SessionEnvelopeJson, HttpStatusCode.Created);
        var api = CreateApi(stub);

        await api.CreateAsync(new HermesAgentSessionWrite { Title = "Mobile chat", Model = "test-model" },
            TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.Equal(2, body.Count);
        Assert.Equal("Mobile chat", body["title"]!.GetValue<string>());
        Assert.Equal("test-model", body["model"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Unwraps()
    {
        var stub = new StubHttpMessageHandler(SessionEnvelopeJson);
        var api = CreateApi(stub);

        var session = await api.GetByIdAsync("api_1710000000_a1b2c3d4", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/sessions/api_1710000000_a1b2c3d4",
            stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal("api_1710000000_a1b2c3d4", session.Id);
    }

    [Fact]
    public async Task UpdateAsync_Patches_Title_And_End_Reason()
    {
        var stub = new StubHttpMessageHandler(SessionEnvelopeJson);
        var api = CreateApi(stub);

        await api.UpdateAsync("api_1710000000_a1b2c3d4",
            new HermesAgentSessionUpdate { Title = "Renamed", EndReason = "wrapped up" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Patch, stub.LastRequest?.Method);
        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.Equal("Renamed", body["title"]!.GetValue<string>());
        Assert.Equal("wrapped up", body["end_reason"]!.GetValue<string>());
    }

    [Fact]
    public async Task DeleteAsync_Sends_Delete_To_Correct_Path()
    {
        var stub = new StubHttpMessageHandler(
            """{ "object": "hermes.session.deleted", "id": "api_1710000000_a1b2c3d4", "deleted": true }""");
        var api = CreateApi(stub);

        await api.DeleteAsync("api_1710000000_a1b2c3d4", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/sessions/api_1710000000_a1b2c3d4",
            stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Delete, stub.LastRequest?.Method);
    }

    [Fact]
    public async Task GetMessagesAsync_Parses_Messages_Including_Tool_Calls()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "object": "list",
              "session_id": "tip-session",
              "data": [
                {
                  "id": 1, "session_id": "tip-session", "role": "user", "content": "hello from phone",
                  "tool_call_id": null, "tool_calls": null, "tool_name": null,
                  "timestamp": 1710000001.0, "token_count": 4, "finish_reason": null,
                  "reasoning": null, "reasoning_content": null
                },
                {
                  "id": 2, "session_id": "tip-session", "role": "assistant", "content": "Let me search:",
                  "tool_calls": [{ "id": "call_1", "type": "function", "function": { "name": "web_search", "arguments": "{}" } }],
                  "timestamp": 1710000002.0
                },
                {
                  "id": 3, "session_id": "tip-session", "role": "tool", "content": "results",
                  "tool_call_id": "call_1", "tool_name": "web_search", "timestamp": 1710000003.0
                }
              ]
            }
            """);
        var api = CreateApi(stub);

        var result = await api.GetMessagesAsync("root-session", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/sessions/root-session/messages",
            stub.LastRequest?.RequestUri?.ToString());
        // The resolved session id may differ from the requested one (compression tip) — surface it.
        Assert.Equal("tip-session", result.SessionId);
        Assert.Equal(3, result.Data.Count);

        Assert.Equal("user", result.Data[0].Role);
        Assert.Equal("hello from phone", result.Data[0].Content?.GetString());
        Assert.Equal(4, result.Data[0].TokenCount);

        var toolCall = Assert.Single(result.Data[1].ToolCalls!);
        Assert.Equal("call_1", toolCall.Id);
        Assert.Equal("web_search", toolCall.Function?.Name);

        Assert.Equal("tool", result.Data[2].Role);
        Assert.Equal("call_1", result.Data[2].ToolCallId);
        Assert.Equal("web_search", result.Data[2].ToolName);
    }

    [Fact]
    public async Task ForkAsync_Defaults_To_Empty_Object_Body_And_Unwraps_Fork()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "object": "hermes.session",
              "session": { "id": "api_1710000099_deadbeef", "source": "api_server", "parent_session_id": "api_1710000000_a1b2c3d4", "title": "Alternative", "started_at": 1710000099.0, "has_system_prompt": false, "has_model_config": false }
            }
            """, HttpStatusCode.Created);
        var api = CreateApi(stub);

        var fork = await api.ForkAsync("api_1710000000_a1b2c3d4",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/sessions/api_1710000000_a1b2c3d4/fork",
            stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal("{}", stub.LastRequestBody); // fork requires a JSON object body even for defaults
        Assert.Equal("api_1710000099_deadbeef", fork.Id);
        Assert.Equal("api_1710000000_a1b2c3d4", fork.ParentSessionId);
    }

    [Fact]
    public async Task ChatAsync_Posts_Message_And_Parses_Completion()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "object": "hermes.session.chat.completion",
              "session_id": "api_1710000000_a1b2c3d4",
              "message": { "role": "assistant", "content": "fresh answer" },
              "usage": { "input_tokens": 3, "output_tokens": 2, "total_tokens": 5 }
            }
            """);
        var api = CreateApi(stub);

        var completion = await api.ChatAsync("api_1710000000_a1b2c3d4",
            new HermesAgentSessionChatRequest { Message = "hello", SystemMessage = "Be nice." },
            new HermesAgentRequestHeaders { SessionKey = "channel-7" },
            TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/sessions/api_1710000000_a1b2c3d4/chat",
            stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal("channel-7", Assert.Single(stub.LastRequest!.Headers.GetValues("X-Hermes-Session-Key")));

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.Equal("hello", body["message"]!.GetValue<string>());
        Assert.Equal("Be nice.", body["system_message"]!.GetValue<string>());

        Assert.Equal("hermes.session.chat.completion", completion.Object);
        Assert.Equal("api_1710000000_a1b2c3d4", completion.SessionId);
        Assert.Equal("assistant", completion.Message?.Role);
        Assert.Equal("fresh answer", completion.Message?.Content);
        Assert.Equal(5, completion.Usage?.TotalTokens);
    }

    [Fact]
    public async Task StreamChatAsync_Parses_Full_Event_Lifecycle_And_Unknown_Fallback()
    {
        // The full session-chat SSE lifecycle from the sessions wire spec, plus an unknown event.
        const string sse =
            """
            event: run.started
            data: {"user_message":{"role":"user","content":"hello"},"session_id":"s1","run_id":"run_1","seq":1,"ts":1710000001.0}

            event: message.started
            data: {"message":{"id":"msg_1","role":"assistant"},"session_id":"s1","run_id":"run_1","seq":2,"ts":1710000001.1}

            event: assistant.delta
            data: {"message_id":"msg_1","delta":"Let me","session_id":"s1","run_id":"run_1","seq":3,"ts":1710000001.2}

            event: tool.progress
            data: {"message_id":"msg_1","tool_name":"_thinking","delta":"pondering","session_id":"s1","run_id":"run_1","seq":4,"ts":1710000001.3}

            event: tool.started
            data: {"message_id":"msg_1","tool_name":"web_search","preview":"searching","args":{"query":"news"},"session_id":"s1","run_id":"run_1","seq":5,"ts":1710000001.4}

            event: tool.completed
            data: {"message_id":"msg_1","tool_name":"web_search","preview":null,"args":null,"session_id":"s1","run_id":"run_1","seq":6,"ts":1710000001.5}

            event: tool.failed
            data: {"message_id":"msg_1","tool_name":"terminal","preview":"boom","args":null,"session_id":"s1","run_id":"run_1","seq":7,"ts":1710000001.6}

            event: assistant.completed
            data: {"message_id":"msg_1","content":"Here is the summary.","completed":true,"partial":false,"interrupted":false,"session_id":"s1-rotated","run_id":"run_1","seq":8,"ts":1710000001.7}

            event: run.completed
            data: {"message_id":"msg_1","completed":true,"messages":[{"role":"assistant","content":"Let me search for that:","tool_calls":[{"id":"call_1","type":"function","function":{"name":"web_search","arguments":"{}"}}]},{"role":"tool","content":"results","tool_call_id":"call_1","tool_name":"web_search"},{"role":"assistant","content":"Here is the summary."}],"usage":{"input_tokens":10,"output_tokens":20,"total_tokens":30},"session_id":"s1-rotated","run_id":"run_1","seq":9,"ts":1710000001.8}

            event: sparkle.magic
            data: {"unicorns":true}

            event: done
            data: {"session_id":"s1","run_id":"run_1","seq":10,"ts":1710000001.9}


            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        var events = new List<HermesAgentSessionChatEvent>();
        await foreach (var chatEvent in api.StreamChatAsync("s1",
                           new HermesAgentSessionChatRequest { Message = "hello" },
                           cancellationToken: TestContext.Current.CancellationToken))
            events.Add(chatEvent);

        Assert.Equal("http://localhost:8642/api/sessions/s1/chat/stream",
            stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(11, events.Count);

        var started = Assert.IsType<HermesAgentSessionChatRunStartedEvent>(events[0]);
        Assert.Equal("run_1", started.RunId);
        Assert.Equal(1, started.Seq);
        Assert.Equal("user", started.UserMessage?.Role);
        Assert.Equal("hello", started.UserMessage?.Content?.GetString());

        var messageStarted = Assert.IsType<HermesAgentSessionChatMessageStartedEvent>(events[1]);
        Assert.Equal("msg_1", messageStarted.Message?.Id);

        var delta = Assert.IsType<HermesAgentSessionChatAssistantDeltaEvent>(events[2]);
        Assert.Equal("Let me", delta.Delta);

        var progress = Assert.IsType<HermesAgentSessionChatToolProgressEvent>(events[3]);
        Assert.Equal("_thinking", progress.ToolName);
        Assert.Equal("pondering", progress.Delta);

        var toolStarted = Assert.IsType<HermesAgentSessionChatToolStartedEvent>(events[4]);
        Assert.Equal("web_search", toolStarted.ToolName);
        Assert.Equal("searching", toolStarted.Preview);
        Assert.Equal("news", toolStarted.Args!.Value.GetProperty("query").GetString());

        Assert.IsType<HermesAgentSessionChatToolCompletedEvent>(events[5]);
        var toolFailed = Assert.IsType<HermesAgentSessionChatToolFailedEvent>(events[6]);
        Assert.Equal("terminal", toolFailed.ToolName);

        var assistantCompleted = Assert.IsType<HermesAgentSessionChatAssistantCompletedEvent>(events[7]);
        Assert.Equal("Here is the summary.", assistantCompleted.Content);
        Assert.Equal("s1-rotated", assistantCompleted.SessionId); // effective (rotated) id on this event
        Assert.True(assistantCompleted.Completed);

        var runCompleted = Assert.IsType<HermesAgentSessionChatRunCompletedEvent>(events[8]);
        Assert.Equal(3, runCompleted.Messages.Count);
        Assert.Equal("assistant", runCompleted.Messages[0].Role);
        Assert.Equal("web_search", Assert.Single(runCompleted.Messages[0].ToolCalls!).Function?.Name);
        Assert.Equal("tool", runCompleted.Messages[1].Role);
        Assert.Equal(30, runCompleted.Usage?.TotalTokens);

        var unknown = Assert.IsType<HermesAgentSessionChatUnknownEvent>(events[9]);
        Assert.Equal("sparkle.magic", unknown.EventType);
        Assert.Equal("""{"unicorns":true}""", unknown.Data);

        var done = Assert.IsType<HermesAgentSessionChatDoneEvent>(events[10]);
        Assert.Equal(10, done.Seq);
    }

    [Fact]
    public async Task StreamChatAsync_Error_Event_And_Unparseable_Payload_Degrade_Gracefully()
    {
        const string sse =
            """
            event: error
            data: {"message":"agent exploded","session_id":"s1","run_id":"run_1","seq":1,"ts":1710000001.0}

            event: assistant.delta
            data: not json

            event: done
            data: {"session_id":"s1","run_id":"run_1","seq":2,"ts":1710000002.0}


            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        var events = new List<HermesAgentSessionChatEvent>();
        await foreach (var chatEvent in api.StreamChatAsync("s1",
                           new HermesAgentSessionChatRequest { Message = "boom" },
                           cancellationToken: TestContext.Current.CancellationToken))
            events.Add(chatEvent);

        Assert.Equal(3, events.Count);
        var error = Assert.IsType<HermesAgentSessionChatErrorEvent>(events[0]);
        Assert.Equal("agent exploded", error.Message);

        // A known event whose payload does not parse degrades to the unknown fallback instead of throwing.
        var unknown = Assert.IsType<HermesAgentSessionChatUnknownEvent>(events[1]);
        Assert.Equal("assistant.delta", unknown.EventType);
        Assert.Equal("not json", unknown.Data);

        Assert.IsType<HermesAgentSessionChatDoneEvent>(events[2]);
    }

    [Fact]
    public void StreamChatAsync_Validates_Arguments_Eagerly()
    {
        // Invalid arguments must throw at the CALL site, not on the first MoveNextAsync — matching the
        // eager-wrapper pattern of the other stream methods.
        var api = CreateApi(new StubHttpMessageHandler("unused", mediaType: "text/event-stream"));

        Assert.Throws<ArgumentException>(() => api.StreamChatAsync(" ",
            new HermesAgentSessionChatRequest { Message = "Hi" },
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => api.StreamChatAsync("s1", null!,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChatAsync_Serializes_Multimodal_Message_Parts()
    {
        var stub = new StubHttpMessageHandler(
            """{ "object": "hermes.session.chat.completion", "session_id": "s1", "message": { "role": "assistant", "content": "a cat" } }""");
        var api = CreateApi(stub);

        await api.ChatAsync("s1", new HermesAgentSessionChatRequest
        {
            Message = HermesAgentMessageContent.FromParts(
                new HermesAgentMessageContentPart { Type = "input_text", Text = "What's in this image?" },
                new HermesAgentMessageContentPart
                {
                    Type = "input_image",
                    ImageUrl = new HermesAgentImageUrl { Url = "data:image/png;base64,AAAA" }
                })
        }, cancellationToken: TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        var parts = body["message"]!.AsArray();
        Assert.Equal("input_text", parts[0]!["type"]!.GetValue<string>());
        Assert.Equal("input_image", parts[1]!["type"]!.GetValue<string>());
        Assert.Equal("data:image/png;base64,AAAA", parts[1]!["image_url"]!["url"]!.GetValue<string>());
    }

    [Fact]
    public async Task CreateAsync_Throws_On_An_Empty_Envelope()
    {
        // A success body without the `session` member must surface as a clear operation-specific exception,
        // not a NullReferenceException or a null return from a non-nullable API.
        var stub = new StubHttpMessageHandler("{}", HttpStatusCode.Created);
        var api = CreateApi(stub);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.CreateAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("no created session", exception.Message);
    }

    [Fact]
    public async Task ForkAsync_Throws_On_An_Empty_Envelope()
    {
        var stub = new StubHttpMessageHandler("""{ "session": null }""", HttpStatusCode.Created);
        var api = CreateApi(stub);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.ForkAsync("api_1710000000_a1b2c3d4", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("no forked session", exception.Message);
    }

    [Fact]
    public async Task GetByIdAsync_Escapes_The_Session_Id_In_The_Request_Path()
    {
        // Ids from other channels (and fork, which skips the create-time path-unsafety checks) are not
        // guaranteed URL-safe — SessionUri must escape them instead of letting them mutate the path.
        var stub = new StubHttpMessageHandler(SessionEnvelopeJson);
        var api = CreateApi(stub);

        await api.GetByIdAsync("abc/12 3#x", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/sessions/abc%2F12%203%23x",
            stub.LastRequest?.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task GetMessagesAsync_Appends_The_Suffix_After_The_Escaped_Session_Id()
    {
        var stub = new StubHttpMessageHandler(
            """{ "object": "list", "session_id": "s1", "data": [] }""");
        var api = CreateApi(stub);

        await api.GetMessagesAsync("abc/12 3#x", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/api/sessions/abc%2F12%203%23x/messages",
            stub.LastRequest?.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task StreamChatAsync_Applies_Optional_Per_Call_Headers()
    {
        // The streaming path applies per-call headers through its OWN request construction (SendSseAsync) —
        // session continuity/idempotency must survive on the stream endpoint too.
        const string sse =
            """
            event: done
            data: {"session_id":"s1","run_id":"run_1","seq":1,"ts":1710000001.0}


            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        await foreach (var _ in api.StreamChatAsync("s1",
                           new HermesAgentSessionChatRequest { Message = "hello" },
                           new HermesAgentRequestHeaders
                           {
                               SessionKey = "channel-42",
                               IdempotencyKey = "idem-1"
                           },
                           TestContext.Current.CancellationToken))
        {
        }

        var headers = stub.LastRequest!.Headers;
        Assert.Equal("channel-42", Assert.Single(headers.GetValues("X-Hermes-Session-Key")));
        Assert.Equal("idem-1", Assert.Single(headers.GetValues("Idempotency-Key")));
    }
}
