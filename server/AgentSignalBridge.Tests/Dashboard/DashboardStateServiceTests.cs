using AgentSignalBridge.Server.Dashboard;
using AgentSignalBridge.Server.Models;
using AgentSignalBridge.Server.Tests.Endpoints;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSignalBridge.Server.Tests.Dashboard;

public sealed class DashboardStateServiceTests
{
    [Fact]
    public async Task SendEventAsync_CreatesAgentSession()
    {
        await using var factory = new TestWebApplicationFactory();
        var service = factory.Services.GetRequiredService<DashboardStateService>();
        var store = factory.Services.GetRequiredService<IAgentStore>();

        await service.SendEventAsync("agent-1", "kimi", AgentEvent.SessionStart);

        Assert.True(store.TryGet("agent-1", out var agent));
        Assert.NotNull(agent);
        Assert.Equal("agent-1", agent.AgentId);
        Assert.Equal("kimi", agent.AgentName);
        Assert.Equal(AgentEvent.SessionStart, agent.Event);
    }

    [Fact]
    public async Task SetMasterAsync_ClaimsMaster_WhenAgentExists()
    {
        await using var factory = new TestWebApplicationFactory();
        var service = factory.Services.GetRequiredService<DashboardStateService>();
        var store = factory.Services.GetRequiredService<IAgentStore>();

        await service.SendEventAsync("agent-1", "kimi", AgentEvent.SessionStart);
        await service.SetMasterAsync("agent-1", true);

        Assert.Equal("agent-1", store.GetMasterAgentId());
    }

    [Fact]
    public async Task SetMasterAsync_Release_ReleasesMaster()
    {
        await using var factory = new TestWebApplicationFactory();
        var service = factory.Services.GetRequiredService<DashboardStateService>();
        var store = factory.Services.GetRequiredService<IAgentStore>();

        await service.SendEventAsync("agent-1", "kimi", AgentEvent.SessionStart);
        await service.SetMasterAsync("agent-1", true);
        await service.SetMasterAsync("agent-1", false);

        Assert.Null(store.GetMasterAgentId());
    }

    [Fact]
    public async Task SetMasterAsync_Claim_DoesNotSetMaster_WhenAgentDoesNotExist()
    {
        await using var factory = new TestWebApplicationFactory();
        var service = factory.Services.GetRequiredService<DashboardStateService>();
        var store = factory.Services.GetRequiredService<IAgentStore>();

        await service.SetMasterAsync("missing-agent", true);

        Assert.Null(store.GetMasterAgentId());
    }
}
