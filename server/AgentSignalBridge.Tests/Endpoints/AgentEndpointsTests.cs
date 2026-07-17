using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentSignalBridge.Server.Api;
using AgentSignalBridge.Server.Drivers;
using AgentSignalBridge.Server.Models;
using AgentSignalBridge.Server.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSignalBridge.Server.Tests.Endpoints;

public sealed class AgentEndpointsTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AgentEndpointsTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task PostHook_CreatesSession_AndReturnsNotMaster()
    {
        var request = new HookRequest
        {
            AgentId = "agent-1",
            AgentName = "kimi",
            Event = AgentEvent.SessionStart
        };

        var response = await _client.PostAsJsonAsync("/hook", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HookResponse>();
        Assert.NotNull(result);
        Assert.True(result.Ok);
        Assert.Equal("agent-1", result.AgentId);
        Assert.False(result.IsMaster);
        Assert.Null(result.Command);
    }

    [Fact]
    public async Task PostHook_WithInvalidEvent_ReturnsBadRequest()
    {
        var request = new HookRequest
        {
            AgentId = "agent-1",
            AgentName = "kimi",
            Event = (AgentEvent)999
        };

        var response = await _client.PostAsJsonAsync("/hook", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMaster_ClaimsMaster_WhenFree()
    {
        await UpsertAgentAsync("agent-1");

        var response = await _client.PostAsJsonAsync("/api/master", new MasterRequest
        {
            AgentId = "agent-1",
            Enabled = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MasterResponse>();
        Assert.NotNull(result);
        Assert.True(result.Ok);
        Assert.True(result.IsMaster);
    }

    [Fact]
    public async Task PostMaster_FromSecondAgent_ReturnsConflict()
    {
        await UpsertAgentAsync("agent-1");
        await UpsertAgentAsync("agent-2");
        await ClaimMasterAsync("agent-1");

        var response = await _client.PostAsJsonAsync("/api/master", new MasterRequest
        {
            AgentId = "agent-2",
            Enabled = true
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MasterResponse>();
        Assert.NotNull(result);
        Assert.False(result.Ok);
        Assert.Equal("master_already_held", result.Error);
        Assert.Equal("agent-1", result.MasterAgentId);
    }

    [Fact]
    public async Task PostMaster_Release_ReleasesMaster()
    {
        await UpsertAgentAsync("agent-1");
        await ClaimMasterAsync("agent-1");

        var response = await _client.PostAsJsonAsync("/api/master", new MasterRequest
        {
            AgentId = "agent-1",
            Enabled = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MasterResponse>();
        Assert.NotNull(result);
        Assert.True(result.Ok);
        Assert.False(result.IsMaster);
    }

    [Fact]
    public async Task PostHook_WhenMaster_SendsCommand()
    {
        await UpsertAgentAsync("agent-1");
        await ClaimMasterAsync("agent-1");

        var response = await _client.PostAsJsonAsync("/hook", new HookRequest
        {
            AgentId = "agent-1",
            AgentName = "kimi",
            Event = AgentEvent.UserPromptSubmit
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HookResponse>();
        Assert.NotNull(result);
        Assert.True(result.IsMaster);
        Assert.Equal(AgentCoreLightCommand.Thinking, result.Command);

        var driver = _factory.GetDriver();
        Assert.Single(driver.Commands);
        Assert.Equal(AgentCoreLightCommand.Thinking, driver.Commands[0]);
    }

    [Fact]
    public async Task PostHeartbeat_KeepsSessionAlive()
    {
        await UpsertAgentAsync("agent-1");

        var response = await _client.PostAsJsonAsync("/heartbeat", new HeartbeatRequest
        {
            AgentId = "agent-1",
            AgentName = "kimi"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<HeartbeatResponse>();
        Assert.NotNull(result);
        Assert.True(result.Ok);
        Assert.Equal("agent-1", result.AgentId);
        Assert.False(result.IsMaster);
    }

    [Fact]
    public async Task GetStatus_ReturnsCurrentState()
    {
        await UpsertAgentAsync("agent-1");
        await ClaimMasterAsync("agent-1");
        await _client.PostAsJsonAsync("/hook", new HookRequest
        {
            AgentId = "agent-1",
            AgentName = "kimi",
            Event = AgentEvent.UserPromptSubmit
        });

        var response = await _client.GetAsync("/api/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StatusResponse>();
        Assert.NotNull(result);
        Assert.True(result.Ok);
        Assert.Equal(AgentCoreLightCommand.Thinking, result.Command);
        Assert.Equal("agent-1", result.MasterAgentId);
    }

    [Fact]
    public async Task GetSessions_ReturnsActiveSessions()
    {
        await UpsertAgentAsync("agent-1");
        await UpsertAgentAsync("agent-2");
        await ClaimMasterAsync("agent-1");

        var response = await _client.GetAsync("/api/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SessionsResponse>();
        Assert.NotNull(result);
        Assert.True(result.Ok);
        Assert.Equal("agent-1", result.MasterAgentId);
        Assert.Equal(2, result.Sessions.Count);
    }

    private async Task UpsertAgentAsync(string agentId)
    {
        var response = await _client.PostAsJsonAsync("/hook", new HookRequest
        {
            AgentId = agentId,
            AgentName = "kimi",
            Event = AgentEvent.SessionStart
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task ClaimMasterAsync(string agentId)
    {
        var response = await _client.PostAsJsonAsync("/api/master", new MasterRequest
        {
            AgentId = agentId,
            Enabled = true
        });
        response.EnsureSuccessStatusCode();
    }
}

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly FakeAgentCoreLightDriver _driver = new();

    public FakeAgentCoreLightDriver GetDriver() => _driver;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgentCoreLightDriver>();
            services.AddSingleton<IAgentCoreLightDriver>(_driver);
        });
    }
}
