using System.Text.Json.Nodes;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using ES.FX.NousResearch.HermesAgent.Responses;
using ES.FX.NousResearch.HermesAgent.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.NousResearch.HermesAgent.Tests.Responses;

public class HermesAgentResponsesApiTests
{
    private static HermesAgentResponsesApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8642/") },
            NullLogger<HermesAgentResponsesApi>.Instance);

    [Fact]
    public async Task CreateAsync_Posts_String_Input_And_Parses_Output_Items()
    {
        // Non-streaming envelope from the wire spec: function_call, function_call_output (output is a plain
        // STRING on this path) and the final assistant message.
        var stub = new StubHttpMessageHandler(
            """
            {
              "id": "resp_0123456789abcdef0123456789ab",
              "object": "response",
              "status": "completed",
              "created_at": 1751700000,
              "model": "hermes-agent",
              "output": [
                { "type": "function_call", "name": "web_search", "arguments": "{\"query\":\"weather\"}", "call_id": "call_1" },
                { "type": "function_call_output", "call_id": "call_1", "output": "sunny, 21C" },
                { "type": "message", "role": "assistant", "content": [{ "type": "output_text", "text": "It is sunny." }] }
              ],
              "usage": { "input_tokens": 9, "output_tokens": 7, "total_tokens": 16 }
            }
            """);
        var api = CreateApi(stub);

        var response = await api.CreateAsync(new HermesAgentResponseRequest
        {
            Input = "What's the weather?",
            Instructions = "Be brief.",
            Store = false
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/responses", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, stub.LastRequest?.Method);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.Equal("What's the weather?", body["input"]!.GetValue<string>()); // string form → JSON string
        Assert.Equal("Be brief.", body["instructions"]!.GetValue<string>());
        Assert.False(body["store"]!.GetValue<bool>());
        Assert.False(body["stream"]!.GetValue<bool>()); // CreateAsync stamps stream:false
        Assert.False(body.ContainsKey("model")); // unset properties are omitted

        Assert.Equal("resp_0123456789abcdef0123456789ab", response.Id);
        Assert.Equal("completed", response.Status);
        Assert.Equal(3, response.Output.Count);

        Assert.Equal("function_call", response.Output[0].Type);
        Assert.Equal("web_search", response.Output[0].Name);
        Assert.Equal("""{"query":"weather"}""", response.Output[0].Arguments);
        Assert.Equal("call_1", response.Output[0].CallId);

        Assert.Equal("function_call_output", response.Output[1].Type);
        Assert.Equal("sunny, 21C", response.Output[1].Output?.Text); // string wire form
        Assert.Null(response.Output[1].Output?.Parts);

        Assert.Equal("message", response.Output[2].Type);
        Assert.Equal("assistant", response.Output[2].Role);
        var part = Assert.Single(response.Output[2].Content!);
        Assert.Equal("output_text", part.Type);
        Assert.Equal("It is sunny.", part.Text);

        Assert.Equal(16, response.Usage?.TotalTokens);
    }

    [Fact]
    public async Task CreateAsync_Serializes_Message_List_Input_And_Conversation_History()
    {
        var stub = new StubHttpMessageHandler("""{ "id": "resp_1", "object": "response", "status": "completed" }""");
        var api = CreateApi(stub);

        await api.CreateAsync(new HermesAgentResponseRequest
        {
            Input = HermesAgentResponseInput.FromMessages(
            [
                new HermesAgentResponseInputMessage { Role = "user", Content = "first" },
                new HermesAgentResponseInputMessage
                {
                    Role = "user",
                    Content = HermesAgentResponseInputContent.FromParts(
                    [
                        new HermesAgentResponseInputTextPart { Text = "look at this" },
                        new HermesAgentResponseInputImagePart { ImageUrl = "https://example.com/cat.png" }
                    ])
                }
            ]),
            ConversationHistory =
            [
                new HermesAgentResponseInputMessage { Role = "assistant", Content = "earlier reply" }
            ]
        }, cancellationToken: TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        var input = body["input"]!.AsArray();
        Assert.Equal("user", input[0]!["role"]!.GetValue<string>());
        Assert.Equal("first", input[0]!["content"]!.GetValue<string>());
        var parts = input[1]!["content"]!.AsArray();
        Assert.Equal("input_text", parts[0]!["type"]!.GetValue<string>());
        Assert.Equal("look at this", parts[0]!["text"]!.GetValue<string>());
        Assert.Equal("input_image", parts[1]!["type"]!.GetValue<string>());
        Assert.Equal("https://example.com/cat.png", parts[1]!["image_url"]!.GetValue<string>());

        var history = body["conversation_history"]!.AsArray();
        Assert.Equal("assistant", history[0]!["role"]!.GetValue<string>());
        Assert.Equal("earlier reply", history[0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "id": "resp_42", "object": "response", "status": "incomplete", "created_at": 1751700001, "model": "hermes-agent", "output": [] }""");
        var api = CreateApi(stub);

        var response = await api.GetByIdAsync("resp_42", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/responses/resp_42", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Get, stub.LastRequest?.Method);
        Assert.Equal("incomplete", response.Status);
        Assert.Empty(response.Output);
    }

    [Fact]
    public async Task DeleteAsync_Sends_Delete_To_Correct_Path()
    {
        var stub = new StubHttpMessageHandler("""{ "id": "resp_42", "object": "response", "deleted": true }""");
        var api = CreateApi(stub);

        await api.DeleteAsync("resp_42", TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/responses/resp_42", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Delete, stub.LastRequest?.Method);
    }

    [Fact]
    public async Task StreamAsync_Parses_Typed_Event_Lifecycle_And_Unknown_Fallback()
    {
        // Named-event SSE per the wire spec; the stream ends with the terminal named event (no [DONE]).
        // function_call_output.output is a PART ARRAY on the streaming path.
        const string sse =
            """
            event: response.created
            data: {"type":"response.created","response":{"id":"resp_1","object":"response","status":"in_progress","created_at":1751700000,"model":"hermes-agent","output":[]},"sequence_number":0}

            event: response.output_item.added
            data: {"type":"response.output_item.added","output_index":0,"item":{"id":"msg_0123456789abcdef01234567","type":"message","status":"in_progress","role":"assistant","content":[]},"sequence_number":1}

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","item_id":"msg_0123456789abcdef01234567","output_index":0,"content_index":0,"delta":"It is","logprobs":[],"sequence_number":2}

            event: response.output_item.done
            data: {"type":"response.output_item.done","output_index":1,"item":{"id":"fco_0123456789abcdef01234567","type":"function_call_output","call_id":"call_1","output":[{"type":"input_text","text":"sunny"}],"status":"completed"},"sequence_number":3}

            event: response.output_text.done
            data: {"type":"response.output_text.done","item_id":"msg_0123456789abcdef01234567","output_index":0,"content_index":0,"text":"It is sunny.","logprobs":[],"sequence_number":4}

            event: response.wildly_new
            data: {"type":"response.wildly_new","sequence_number":5}

            event: response.completed
            data: {"type":"response.completed","response":{"id":"resp_1","object":"response","status":"completed","created_at":1751700000,"model":"hermes-agent","output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"It is sunny."}]}],"usage":{"input_tokens":3,"output_tokens":4,"total_tokens":7}},"sequence_number":6}


            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        var events = new List<HermesAgentResponseStreamEvent>();
        await foreach (var streamEvent in api.StreamAsync(new HermesAgentResponseRequest { Input = "weather?" },
                           cancellationToken: TestContext.Current.CancellationToken))
            events.Add(streamEvent);

        Assert.Equal(7, events.Count);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.True(body["stream"]!.GetValue<bool>()); // StreamAsync stamps stream:true

        var created = Assert.IsType<HermesAgentResponseCreatedEvent>(events[0]);
        Assert.Equal("in_progress", created.Response?.Status);
        Assert.Equal(0, created.SequenceNumber);

        var added = Assert.IsType<HermesAgentResponseOutputItemAddedEvent>(events[1]);
        Assert.Equal("message", added.Item?.Type);
        Assert.Equal("in_progress", added.Item?.Status);
        Assert.Equal(0, added.OutputIndex);

        var delta = Assert.IsType<HermesAgentResponseOutputTextDeltaEvent>(events[2]);
        Assert.Equal("It is", delta.Delta);
        Assert.Equal("msg_0123456789abcdef01234567", delta.ItemId);

        var itemDone = Assert.IsType<HermesAgentResponseOutputItemDoneEvent>(events[3]);
        Assert.Equal("function_call_output", itemDone.Item?.Type);
        var outputPart = Assert.Single(itemDone.Item!.Output!.Parts!); // part-array wire form
        Assert.Equal("input_text", outputPart.Type);
        Assert.Equal("sunny", outputPart.Text);
        Assert.Null(itemDone.Item.Output!.Text);

        var textDone = Assert.IsType<HermesAgentResponseOutputTextDoneEvent>(events[4]);
        Assert.Equal("It is sunny.", textDone.Text);

        var unknown = Assert.IsType<HermesAgentResponseUnknownEvent>(events[5]);
        Assert.Equal("response.wildly_new", unknown.EventType);

        var completed = Assert.IsType<HermesAgentResponseCompletedEvent>(events[6]);
        Assert.Equal("completed", completed.Response?.Status);
        Assert.Equal(7, completed.Response?.Usage?.TotalTokens);
        Assert.Equal(6, completed.SequenceNumber);
    }

    [Fact]
    public async Task StreamAsync_Failed_Terminal_Event_And_Unparseable_Known_Event_Degrade_Gracefully()
    {
        const string sse =
            """
            event: response.created
            data: this payload is not json

            event: response.failed
            data: {"type":"response.failed","response":{"id":"resp_1","object":"response","status":"failed","output":[],"error":{"message":"agent failed","type":"server_error"}},"sequence_number":1}


            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        var events = new List<HermesAgentResponseStreamEvent>();
        await foreach (var streamEvent in api.StreamAsync(new HermesAgentResponseRequest { Input = "boom" },
                           cancellationToken: TestContext.Current.CancellationToken))
            events.Add(streamEvent);

        Assert.Equal(2, events.Count);

        // A known event name whose payload no longer parses must degrade to the unknown fallback, not throw.
        var unknown = Assert.IsType<HermesAgentResponseUnknownEvent>(events[0]);
        Assert.Equal("response.created", unknown.EventType);
        Assert.Equal("this payload is not json", unknown.Data);

        var failed = Assert.IsType<HermesAgentResponseFailedEvent>(events[1]);
        Assert.Equal("failed", failed.Response?.Status);
        Assert.Equal("agent failed", failed.Response?.Error?.Message);
        Assert.Equal("server_error", failed.Response?.Error?.Type);
    }

    [Fact]
    public async Task CreateAsync_Surfaces_Effective_Session_Id_From_Response_Header()
    {
        // The effective (derived/rotated) session id exists ONLY on the X-Hermes-Session-Id response header —
        // the response envelope body never carries it.
        var stub = new StubHttpMessageHandler(
            """{ "id": "resp_1", "object": "response", "status": "completed", "output": [] }""",
            responseHeaders: new Dictionary<string, string> { ["X-Hermes-Session-Id"] = "api-1a2b3c4d5e6f7a8b" });
        var api = CreateApi(stub);

        var response = await api.CreateAsync(new HermesAgentResponseRequest { Input = "Hi" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("api-1a2b3c4d5e6f7a8b", response.EffectiveSessionId);
    }

    [Fact]
    public async Task StreamAsync_Yields_Stream_Start_Event_First_When_Server_Reports_Session_Id()
    {
        // On SSE the effective session id also exists ONLY on the X-Hermes-Session-Id response header — the
        // client surfaces it as a synthetic stream-start event yielded before any server event.
        const string sse =
            """
            event: response.created
            data: {"type":"response.created","response":{"id":"resp_1","object":"response","status":"in_progress","output":[]},"sequence_number":0}

            event: response.completed
            data: {"type":"response.completed","response":{"id":"resp_1","object":"response","status":"completed","output":[]},"sequence_number":1}


            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream",
            responseHeaders: new Dictionary<string, string> { ["X-Hermes-Session-Id"] = "api-rotated0001" });
        var api = CreateApi(stub);

        var events = new List<HermesAgentResponseStreamEvent>();
        await foreach (var streamEvent in api.StreamAsync(new HermesAgentResponseRequest { Input = "Hi" },
                           cancellationToken: TestContext.Current.CancellationToken))
            events.Add(streamEvent);

        Assert.Equal(3, events.Count);
        var start = Assert.IsType<HermesAgentResponseStreamStartEvent>(events[0]);
        Assert.Equal("api-rotated0001", start.EffectiveSessionId);
        Assert.IsType<HermesAgentResponseCreatedEvent>(events[1]);
        Assert.IsType<HermesAgentResponseCompletedEvent>(events[2]);
    }
}