using AgentTrafficLight.Server.Drivers;
using AgentTrafficLight.Server.Hardware;
using AgentTrafficLight.Server.Models;
using AgentTrafficLight.Server.Stores;
using AgentTrafficLight.Server.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentTrafficLight.Server.Tests.Hardware;

public sealed class AgentCoreLightManagerTests : IDisposable
{
    private readonly InMemoryAgentStore _store = new();
    private readonly FakeAgentCoreLightDriver _driver = new();
    private readonly AgentCoreLightManager _manager;

    public AgentCoreLightManagerTests()
    {
        _manager = new AgentCoreLightManager(_store, _driver, NullLogger<AgentCoreLightManager>.Instance);
    }

    public void Dispose()
    {
        _driver.DisposeAsync().AsTask().Wait();
        _store.Dispose();
    }

    [Theory]
    [InlineData(AgentEvent.SessionStart, TrafficLightCommand.Idle)]
    [InlineData(AgentEvent.UserPromptSubmit, TrafficLightCommand.Thinking)]
    [InlineData(AgentEvent.PreToolUse, TrafficLightCommand.Busy)]
    [InlineData(AgentEvent.PostToolUse, TrafficLightCommand.Thinking)]
    [InlineData(AgentEvent.PermissionRequest, TrafficLightCommand.WaitConfirm)]
    [InlineData(AgentEvent.Stop, TrafficLightCommand.Success)]
    [InlineData(AgentEvent.StopFailure, TrafficLightCommand.Error)]
    [InlineData(AgentEvent.Disconnect, TrafficLightCommand.Off)]
    [InlineData(AgentEvent.SessionEnd, TrafficLightCommand.Off)]
    public async Task OnAgentEventAsync_WhenMaster_MapsEventToCommand(AgentEvent agentEvent, TrafficLightCommand expectedCommand)
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);
        _store.TrySetMaster("agent-1");

        await _manager.OnAgentEventAsync("agent-1", agentEvent, CancellationToken.None);

        Assert.Single(_driver.Commands);
        Assert.Equal(expectedCommand, _driver.Commands[0]);
    }

    [Fact]
    public async Task OnAgentEventAsync_WhenNotMaster_IgnoresEvent()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);
        _store.Upsert("agent-2", "claude", null, AgentEvent.SessionStart, now);
        _store.TrySetMaster("agent-2");

        await _manager.OnAgentEventAsync("agent-1", AgentEvent.PreToolUse, CancellationToken.None);

        Assert.Empty(_driver.Commands);
    }

    [Fact]
    public async Task OnAgentEventAsync_WhenNoMaster_IgnoresEvent()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);

        await _manager.OnAgentEventAsync("agent-1", AgentEvent.PreToolUse, CancellationToken.None);

        Assert.Empty(_driver.Commands);
    }

    [Fact]
    public async Task OnAgentEventAsync_ConnectsDriver_WhenNotConnected()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);
        _store.TrySetMaster("agent-1");
        Assert.False(_driver.IsConnected);

        await _manager.OnAgentEventAsync("agent-1", AgentEvent.SessionStart, CancellationToken.None);

        Assert.True(_driver.IsConnected);
    }

    [Fact]
    public async Task OnAgentEventAsync_SkipsConnection_WhenAlreadyConnected()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);
        _store.TrySetMaster("agent-1");
        await _driver.ConnectAsync();
        _driver.ClearCommands();

        await _manager.OnAgentEventAsync("agent-1", AgentEvent.SessionStart, CancellationToken.None);

        Assert.True(_driver.IsConnected);
        Assert.Single(_driver.Commands);
    }
}
