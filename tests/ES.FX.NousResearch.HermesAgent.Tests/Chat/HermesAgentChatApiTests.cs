using System.Net;
using System.Text.Json.Nodes;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using ES.FX.NousResearch.HermesAgent.Chat;
using ES.FX.NousResearch.HermesAgent.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.NousResearch.HermesAgent.Tests.Chat;

public class HermesAgentChatApiTests
{
    private static HermesAgentChatApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8642/") },
            NullLogger<HermesAgentChatApi>.Instance);

    [Fact]
    public async Task CompleteAsync_Posts_Correct_Path_Body_And_Parses_Completion()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "id": "chatcmpl-0123456789abcdef0123456789abc",
              "object": "chat.completion",
              "created": 1751700000,
              "model": "hermes-agent",
              "choices": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": "Hello there!" },
                  "finish_reason": "stop"
                }
              ],
              "usage": { "prompt_tokens": 12, "completion_tokens": 4, "total_tokens": 16 }
            }
            """);
        var api = CreateApi(stub);

        var completion = await api.CompleteAsync(new HermesAgentChatCompletionRequest
        {
            Messages = [HermesAgentChatMessage.FromUser("Hello")],
            Model = "hermes-agent"
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/chat/completions", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, stub.LastRequest?.Method);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.Equal("user", body["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Equal("Hello", body["messages"]![0]!["content"]!.GetValue<string>());
        Assert.Equal("hermes-agent", body["model"]!.GetValue<string>());
        Assert.False(body["stream"]!.GetValue<bool>()); // CompleteAsync enforces stream:false

        Assert.Equal("chatcmpl-0123456789abcdef0123456789abc", completion.Id);
        Assert.Equal("chat.completion", completion.Object);
        var choice = Assert.Single(completion.Choices);
        Assert.Equal("assistant", choice.Message?.Role);
        Assert.Equal("Hello there!", choice.Message?.Content?.Text);
        Assert.Equal("stop", choice.FinishReason);
        Assert.Equal(12, completion.Usage?.PromptTokens);
        Assert.Equal(4, completion.Usage?.CompletionTokens);
        Assert.Equal(16, completion.Usage?.TotalTokens);
        Assert.Null(completion.Hermes);
        Assert.Null(completion.EffectiveSessionId); // no X-Hermes-Session-Id response header sent
    }

    [Fact]
    public async Task CompleteAsync_Serializes_Multimodal_Content_Parts_And_Parses_Soft_Partial()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "id": "chatcmpl-1",
              "object": "chat.completion",
              "created": 1751700000,
              "model": "hermes-agent",
              "choices": [
                { "index": 0, "message": { "role": "assistant", "content": "partial text" }, "finish_reason": "length" }
              ],
              "usage": { "prompt_tokens": 1, "completion_tokens": 2, "total_tokens": 3 },
              "hermes": { "completed": false, "partial": true, "failed": false, "error": "output truncated", "error_code": "output_truncated" }
            }
            """);
        var api = CreateApi(stub);

        var completion = await api.CompleteAsync(new HermesAgentChatCompletionRequest
        {
            Messages =
            [
                HermesAgentChatMessage.FromUser(HermesAgentMessageContent.FromParts(
                    new HermesAgentMessageContentPart { Type = "text", Text = "What's in this image?" },
                    new HermesAgentMessageContentPart
                    {
                        Type = "image_url",
                        ImageUrl = new HermesAgentImageUrl { Url = "data:image/png;base64,AAAA" }
                    }))
            ]
        }, cancellationToken: TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        var content = body["messages"]![0]!["content"]!.AsArray();
        Assert.Equal("text", content[0]!["type"]!.GetValue<string>());
        Assert.Equal("What's in this image?", content[0]!["text"]!.GetValue<string>());
        Assert.Equal("image_url", content[1]!["type"]!.GetValue<string>());
        Assert.Equal("data:image/png;base64,AAAA", content[1]!["image_url"]!["url"]!.GetValue<string>());

        Assert.Equal("length", completion.Choices[0].FinishReason);
        Assert.False(completion.Hermes?.Completed);
        Assert.True(completion.Hermes?.Partial);
        Assert.Equal("output_truncated", completion.Hermes?.ErrorCode);
    }

    [Fact]
    public async Task CompleteAsync_Applies_Optional_Per_Call_Headers()
    {
        var stub = new StubHttpMessageHandler("""{ "id": "chatcmpl-1", "object": "chat.completion" }""");
        var api = CreateApi(stub);

        await api.CompleteAsync(new HermesAgentChatCompletionRequest
        {
            Messages = [HermesAgentChatMessage.FromUser("Hi")]
        }, new HermesAgentRequestHeaders
        {
            SessionId = "api-abc123",
            SessionKey = "channel-42",
            IdempotencyKey = "idem-1"
        }, TestContext.Current.CancellationToken);

        var headers = stub.LastRequest!.Headers;
        Assert.Equal("api-abc123", Assert.Single(headers.GetValues("X-Hermes-Session-Id")));
        Assert.Equal("channel-42", Assert.Single(headers.GetValues("X-Hermes-Session-Key")));
        Assert.Equal("idem-1", Assert.Single(headers.GetValues("Idempotency-Key")));
    }

    [Fact]
    public async Task CompleteAsync_Surfaces_Effective_Session_Id_From_Response_Header()
    {
        // The effective (derived/rotated) session id exists ONLY on the X-Hermes-Session-Id response header —
        // the completion body never carries it.
        var stub = new StubHttpMessageHandler("""{ "id": "chatcmpl-1", "object": "chat.completion" }""",
            responseHeaders: new Dictionary<string, string> { ["X-Hermes-Session-Id"] = "api-1a2b3c4d5e6f7a8b" });
        var api = CreateApi(stub);

        var completion = await api.CompleteAsync(new HermesAgentChatCompletionRequest
        {
            Messages = [HermesAgentChatMessage.FromUser("Hi")]
        }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("api-1a2b3c4d5e6f7a8b", completion.EffectiveSessionId);
    }

    [Fact]
    public async Task CompleteAsync_Rejects_Per_Call_Header_Values_With_Control_Characters()
    {
        // CRLF in a caller-supplied header value must throw (validated add) instead of being written to the
        // wire as a second header line (request header injection).
        var stub = new StubHttpMessageHandler("""{ "id": "chatcmpl-1", "object": "chat.completion" }""");
        var api = CreateApi(stub);

        await Assert.ThrowsAsync<FormatException>(() => api.CompleteAsync(new HermesAgentChatCompletionRequest
            {
                Messages = [HermesAgentChatMessage.FromUser("Hi")]
            }, new HermesAgentRequestHeaders { SessionKey = "abc\r\nX-Injected: pwned" },
            TestContext.Current.CancellationToken));

        Assert.Null(stub.LastRequest); // nothing reached the wire
    }

    [Fact]
    public async Task StreamAsync_Parses_Chunks_Tool_Progress_And_Stops_At_Done()
    {
        // Exact wire shapes from the chat-completions SSE spec: unnamed data chunks, named
        // hermes.tool.progress events, a keepalive comment, an unknown named event and the [DONE] terminator
        // (anything after it must never be yielded).
        const string sse =
            """
            data: {"id":"chatcmpl-abc","object":"chat.completion.chunk","created":1751700000,"model":"hermes-agent","choices":[{"index":0,"delta":{"role":"assistant"},"finish_reason":null}]}

            data: {"id":"chatcmpl-abc","object":"chat.completion.chunk","created":1751700000,"model":"hermes-agent","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

            event: hermes.tool.progress
            data: {"tool":"web_search","emoji":"🔍","label":"Searching the web","toolCallId":"call_1","status":"running"}

            : keepalive

            event: hermes.tool.progress
            data: {"tool":"web_search","toolCallId":"call_1","status":"completed"}

            event: hermes.future.event
            data: {"anything":"goes"}

            data: {"id":"chatcmpl-abc","object":"chat.completion.chunk","created":1751700000,"model":"hermes-agent","choices":[{"index":0,"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}

            data: [DONE]

            data: {"id":"chatcmpl-after-done","object":"chat.completion.chunk","created":1,"model":"x","choices":[]}

            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        var events = new List<HermesAgentChatStreamEvent>();
        await foreach (var streamEvent in api.StreamAsync(new HermesAgentChatCompletionRequest
                       {
                           Messages = [HermesAgentChatMessage.FromUser("Hello")]
                       }, cancellationToken: TestContext.Current.CancellationToken))
            events.Add(streamEvent);

        Assert.Equal(6, events.Count);

        var body = JsonNode.Parse(stub.LastRequestBody!)!.AsObject();
        Assert.True(body["stream"]!.GetValue<bool>()); // StreamAsync enforces stream:true
        Assert.Equal("http://localhost:8642/v1/chat/completions", stub.LastRequest?.RequestUri?.ToString());

        var role = Assert.IsType<HermesAgentChatCompletionChunkEvent>(events[0]);
        Assert.Equal("assistant", role.Chunk.Choices[0].Delta?.Role);

        var content = Assert.IsType<HermesAgentChatCompletionChunkEvent>(events[1]);
        Assert.Equal("Hello", content.Chunk.Choices[0].Delta?.Content);

        var running = Assert.IsType<HermesAgentToolProgressEvent>(events[2]);
        Assert.Equal("web_search", running.Progress.Tool);
        Assert.Equal("🔍", running.Progress.Emoji);
        Assert.Equal("Searching the web", running.Progress.Label);
        Assert.Equal("call_1", running.Progress.ToolCallId);
        Assert.Equal("running", running.Progress.Status);

        var completed = Assert.IsType<HermesAgentToolProgressEvent>(events[3]);
        Assert.Equal("completed", completed.Progress.Status);
        Assert.Null(completed.Progress.Emoji);

        var unknown = Assert.IsType<HermesAgentChatStreamUnknownEvent>(events[4]);
        Assert.Equal("hermes.future.event", unknown.EventType);
        Assert.Equal("""{"anything":"goes"}""", unknown.Data);

        var final = Assert.IsType<HermesAgentChatCompletionChunkEvent>(events[5]);
        Assert.Equal("stop", final.Chunk.Choices[0].FinishReason);
        Assert.Equal(15, final.Chunk.Usage?.TotalTokens);
    }

    [Fact]
    public async Task StreamAsync_Final_Error_Chunk_Carries_Error_And_Hermes_Extensions()
    {
        const string sse =
            """
            data: {"id":"chatcmpl-x","object":"chat.completion.chunk","created":1751700000,"model":"hermes-agent","choices":[{"index":0,"delta":{},"finish_reason":"error"}],"usage":{"prompt_tokens":1,"completion_tokens":0,"total_tokens":1},"error":{"message":"agent exploded","type":"agent_error"},"hermes":{"completed":false,"partial":false,"failed":true,"error":"agent exploded","error_code":"agent_error"}}

            data: [DONE]

            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        var events = new List<HermesAgentChatStreamEvent>();
        await foreach (var streamEvent in api.StreamAsync(new HermesAgentChatCompletionRequest
                       {
                           Messages = [HermesAgentChatMessage.FromUser("boom")]
                       }, cancellationToken: TestContext.Current.CancellationToken))
            events.Add(streamEvent);

        var final = Assert.IsType<HermesAgentChatCompletionChunkEvent>(Assert.Single(events));
        Assert.Equal("error", final.Chunk.Choices[0].FinishReason);
        Assert.Equal("agent exploded", final.Chunk.Error?.Message);
        Assert.Equal("agent_error", final.Chunk.Error?.Type);
        Assert.True(final.Chunk.Hermes?.Failed);
    }

    [Fact]
    public async Task StreamAsync_Yields_Stream_Start_Event_First_When_Server_Reports_Session_Id()
    {
        // On SSE the effective session id also exists ONLY on the X-Hermes-Session-Id response header — the
        // client surfaces it as a synthetic stream-start event yielded before any server event.
        const string sse =
            """
            data: {"id":"chatcmpl-abc","object":"chat.completion.chunk","created":1751700000,"model":"hermes-agent","choices":[{"index":0,"delta":{"content":"Hi"},"finish_reason":null}]}

            data: [DONE]

            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream",
            responseHeaders: new Dictionary<string, string> { ["X-Hermes-Session-Id"] = "api-rotated0001" });
        var api = CreateApi(stub);

        var events = new List<HermesAgentChatStreamEvent>();
        await foreach (var streamEvent in api.StreamAsync(new HermesAgentChatCompletionRequest
                       {
                           Messages = [HermesAgentChatMessage.FromUser("Hello")]
                       }, cancellationToken: TestContext.Current.CancellationToken))
            events.Add(streamEvent);

        Assert.Equal(2, events.Count);
        var start = Assert.IsType<HermesAgentChatStreamStartEvent>(events[0]);
        Assert.Equal("api-rotated0001", start.EffectiveSessionId);
        var chunk = Assert.IsType<HermesAgentChatCompletionChunkEvent>(events[1]);
        Assert.Equal("Hi", chunk.Chunk.Choices[0].Delta?.Content);
    }

    [Fact]
    public async Task StreamAsync_Undeserializable_Chunk_Falls_Back_To_Unknown_Event()
    {
        const string sse =
            """
            data: this is not json

            data: [DONE]

            """;
        var stub = new StubHttpMessageHandler(sse, mediaType: "text/event-stream");
        var api = CreateApi(stub);

        var events = new List<HermesAgentChatStreamEvent>();
        await foreach (var streamEvent in api.StreamAsync(new HermesAgentChatCompletionRequest
                       {
                           Messages = [HermesAgentChatMessage.FromUser("Hi")]
                       }, cancellationToken: TestContext.Current.CancellationToken))
            events.Add(streamEvent);

        var unknown = Assert.IsType<HermesAgentChatStreamUnknownEvent>(Assert.Single(events));
        Assert.Equal("message", unknown.EventType); // the SSE default event name for unnamed data events
        Assert.Equal("this is not json", unknown.Data);
    }

    [Fact]
    public async Task StreamAsync_Applies_Optional_Per_Call_Headers()
    {
        // The streaming path applies per-call headers through its OWN request construction (SendSseAsync),
        // separate from the non-streaming SendAsync — both must carry session continuity/idempotency headers.
        var stub = new StubHttpMessageHandler("data: [DONE]\n\n", mediaType: "text/event-stream");
        var api = CreateApi(stub);

        await foreach (var _ in api.StreamAsync(new HermesAgentChatCompletionRequest
                       {
                           Messages = [HermesAgentChatMessage.FromUser("Hi")]
                       }, new HermesAgentRequestHeaders
                       {
                           SessionId = "api-abc123",
                           SessionKey = "channel-42",
                           IdempotencyKey = "idem-1"
                       }, TestContext.Current.CancellationToken))
        {
        }

        var headers = stub.LastRequest!.Headers;
        Assert.Equal("api-abc123", Assert.Single(headers.GetValues("X-Hermes-Session-Id")));
        Assert.Equal("channel-42", Assert.Single(headers.GetValues("X-Hermes-Session-Key")));
        Assert.Equal("idem-1", Assert.Single(headers.GetValues("Idempotency-Key")));
    }

    [Fact]
    public async Task StreamAsync_Non_Success_Response_Throws_Api_Exception_Before_Any_Event()
    {
        // The response guard runs on the streaming path BEFORE the first event is yielded — an error JSON body
        // must surface as a typed exception (with parsed error and Retry-After), never as garbage SSE events.
        var stub = new StubHttpMessageHandler(
            """{ "error": { "message": "Too many concurrent requests.", "type": "rate_limit_error" } }""",
            HttpStatusCode.TooManyRequests,
            responseHeaders: new Dictionary<string, string> { ["Retry-After"] = "1" });
        var api = CreateApi(stub);

        var exception = await Assert.ThrowsAsync<HermesAgentApiException>(async () =>
        {
            await foreach (var _ in api.StreamAsync(new HermesAgentChatCompletionRequest
                           {
                               Messages = [HermesAgentChatMessage.FromUser("Hi")]
                           }, cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal("Too many concurrent requests.", exception.Error?.Message);
        Assert.Equal("rate_limit_error", exception.Error?.Type);
        Assert.Equal(TimeSpan.FromSeconds(1), exception.RetryAfter);
    }

    [Fact]
    public async Task StreamAsync_Cancelling_Mid_Stream_Surfaces_OperationCanceledException_Promptly()
    {
        // One complete event, then the stream blocks (keepalive-only live stream). Cancelling the consumer's
        // token must cancel the pending read and surface promptly — a token-drop regression anywhere in the
        // SSE pipeline would fail this test with a TimeoutException instead of hanging.
        const string firstEvent =
            "data: {\"id\":\"chatcmpl-abc\",\"object\":\"chat.completion.chunk\",\"created\":1751700000,\"model\":\"hermes-agent\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hi\"},\"finish_reason\":null}]}\n\n";
        var stream = new SseTestStream(firstEvent, true);
        var api = CreateApi(new SseStreamStubHandler(stream));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await using var events = api.StreamAsync(new HermesAgentChatCompletionRequest
        {
            Messages = [HermesAgentChatMessage.FromUser("Hi")]
        }, cancellationToken: cts.Token).GetAsyncEnumerator(cts.Token);

        Assert.True(await events.MoveNextAsync());
        Assert.IsType<HermesAgentChatCompletionChunkEvent>(events.Current);

        var pending = events.MoveNextAsync();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            pending.AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StreamAsync_Abandoned_By_The_Consumer_Disposes_The_Response()
    {
        // Breaking out of enumeration disposes the iterator, which must dispose the (headers-read) response —
        // otherwise every abandoned stream leaks a pooled connection under IHttpClientFactory.
        const string sse =
            "data: {\"id\":\"chatcmpl-abc\",\"object\":\"chat.completion.chunk\",\"created\":1751700000,\"model\":\"hermes-agent\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hi\"},\"finish_reason\":null}]}\n\n" +
            "data: {\"id\":\"chatcmpl-abc\",\"object\":\"chat.completion.chunk\",\"created\":1751700000,\"model\":\"hermes-agent\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\" there\"},\"finish_reason\":null}]}\n\n";
        var stream = new SseTestStream(sse);
        var api = CreateApi(new SseStreamStubHandler(stream));

        await foreach (var _ in api.StreamAsync(new HermesAgentChatCompletionRequest
                       {
                           Messages = [HermesAgentChatMessage.FromUser("Hi")]
                       }, cancellationToken: TestContext.Current.CancellationToken))
            break; // abandon after the first event

        Assert.True(stream.Disposed);
    }
}