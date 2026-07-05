using ES.FX.NousResearch.HermesAgent.Server;
using ES.FX.NousResearch.HermesAgent.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.NousResearch.HermesAgent.Tests.Server;

public class HermesAgentServerApiTests
{
    private static HermesAgentServerApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8642/") },
            NullLogger<HermesAgentServerApi>.Instance);

    [Fact]
    public async Task GetModelsAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "object": "list",
              "data": [
                { "id": "hermes-agent", "object": "model", "created": 1751700000, "owned_by": "hermes", "permission": [], "root": "hermes-agent", "parent": null },
                { "id": "fast", "object": "model", "created": 1751700000, "owned_by": "hermes", "permission": [], "root": "hermes-4-mini", "parent": "hermes-agent" }
              ]
            }
            """);
        var api = CreateApi(stub);

        var models = await api.GetModelsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/models", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(HttpMethod.Get, stub.LastRequest?.Method);
        Assert.Equal("list", models.Object);
        Assert.Equal(2, models.Data.Count);
        Assert.Equal("hermes-agent", models.Data[0].Id);
        Assert.Null(models.Data[0].Parent);
        Assert.Equal("fast", models.Data[1].Id);
        Assert.Equal("hermes-4-mini", models.Data[1].Root);
        Assert.Equal("hermes-agent", models.Data[1].Parent);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_Parses_Auth_Runtime_Features_And_Endpoints()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "object": "hermes.api_server.capabilities",
              "platform": "hermes-agent",
              "model": "hermes-agent",
              "auth": { "type": "bearer", "required": true },
              "runtime": { "mode": "server_agent", "tool_execution": "server", "split_runtime": false, "description": "..." },
              "features": {
                "chat_completions": true,
                "chat_completions_streaming": true,
                "responses_api": true,
                "session_chat": true,
                "jobs_admin": false,
                "session_continuity_header": "X-Hermes-Session-Id",
                "session_key_header": "X-Hermes-Session-Key",
                "cors": false
              },
              "endpoints": {
                "health": { "method": "GET", "path": "/health" },
                "chat_completions": { "method": "POST", "path": "/v1/chat/completions" }
              }
            }
            """);
        var api = CreateApi(stub);

        var capabilities = await api.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/capabilities", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal("hermes.api_server.capabilities", capabilities.Object);
        Assert.Equal("hermes-agent", capabilities.Platform);
        Assert.Equal("bearer", capabilities.Auth?.Type);
        Assert.True(capabilities.Auth?.Required);
        Assert.Equal("server_agent", capabilities.Runtime?.Mode);
        Assert.True(capabilities.Features?.ChatCompletions);
        Assert.False(capabilities.Features?.JobsAdmin);
        Assert.Equal("X-Hermes-Session-Id", capabilities.Features?.SessionContinuityHeader);
        Assert.Equal("POST", capabilities.Endpoints?["chat_completions"].Method);
        Assert.Equal("/v1/chat/completions", capabilities.Endpoints?["chat_completions"].Path);
    }

    [Fact]
    public async Task GetSkillsAsync_Unwraps_The_List_Envelope()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "object": "list",
              "data": [
                { "name": "research", "description": "Deep research", "category": "knowledge" },
                { "name": "coding", "description": "Write code", "category": "dev" }
              ]
            }
            """);
        var api = CreateApi(stub);

        var skills = await api.GetSkillsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/skills", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal(2, skills.Count);
        Assert.Equal("research", skills[0].Name);
        Assert.Equal("knowledge", skills[0].Category);
    }

    [Fact]
    public async Task GetToolsetsAsync_Unwraps_The_List_Envelope()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "object": "list",
              "platform": "api_server",
              "data": [
                { "name": "files", "label": "Files", "description": "File tools", "enabled": true, "configured": true, "tools": ["read_file", "terminal"] }
              ]
            }
            """);
        var api = CreateApi(stub);

        var toolsets = await api.GetToolsetsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/toolsets", stub.LastRequest?.RequestUri?.ToString());
        var toolset = Assert.Single(toolsets);
        Assert.Equal("files", toolset.Name);
        Assert.True(toolset.Enabled);
        Assert.Equal(new[] { "read_file", "terminal" }, toolset.Tools);
    }

    [Fact]
    public async Task GetHealthAsync_Requests_V1_Health_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "status": "ok", "platform": "hermes-agent", "version": "1.2.3" }""");
        var api = CreateApi(stub);

        var health = await api.GetHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:8642/v1/health", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal("ok", health.Status);
        Assert.Equal("hermes-agent", health.Platform);
        Assert.Equal("1.2.3", health.Version);
    }

    [Fact]
    public async Task GetDetailedHealthAsync_Requests_Root_Scoped_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """
            {
              "status": "ok",
              "platform": "hermes-agent",
              "version": "1.2.3",
              "gateway_state": "running",
              "platforms": { "api_server": { "connected": true } },
              "active_agents": 2,
              "gateway_busy": false,
              "gateway_drainable": true,
              "exit_reason": null,
              "updated_at": "2026-07-05T09:00:00+00:00",
              "pid": 12345
            }
            """);
        var api = CreateApi(stub);

        var health = await api.GetDetailedHealthAsync(TestContext.Current.CancellationToken);

        // QUIRK under test: detailed health lives at the ROOT (`/health/detailed`), not under `/v1`.
        Assert.Equal("http://localhost:8642/health/detailed", stub.LastRequest?.RequestUri?.ToString());
        Assert.Equal("running", health.GatewayState);
        Assert.Equal(2, health.ActiveAgents);
        Assert.False(health.GatewayBusy);
        Assert.Equal(12345, health.Pid);
        Assert.True(health.Platforms?.ContainsKey("api_server"));
    }

    [Fact]
    public async Task GetHealthAsync_Throws_On_A_Null_Success_Body()
    {
        // A 200 whose body is the JSON literal `null` deserializes to null — the shared send flow must throw
        // an operation-named InvalidOperationException, never return null into a non-nullable API.
        var stub = new StubHttpMessageHandler("null");
        var api = CreateApi(stub);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            api.GetHealthAsync(TestContext.Current.CancellationToken));

        Assert.Contains("empty response for 'HermesAgent.Server.GetHealth'", exception.Message);
    }
}
