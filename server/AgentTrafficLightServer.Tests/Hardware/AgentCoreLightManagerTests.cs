using AgentTrafficLight.Server.Drivers;
using AgentTrafficLight.Server.Hardware;
using AgentTrafficLight.Server.Models;
using AgentTrafficLight.Server.Stores;
using AgentTrafficLight.Server.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace AgentTrafficLight.Server.Tests.Hardware;

public sealed class AgentCoreLightManagerTests : IDisposable
{
    private readonly InMemoryAgentStore _store = new();
    private readonly FakeAgentCoreLightDriver _driver = new();
    private readonly FakeLogger<AgentCoreLightManager> _logger = new();
    private readonly AgentCoreLightManager _manager;

    public AgentCoreLightManagerTests()
    {
        _manager = new AgentCoreLightManager(_store, _driver, _logger);
    }

    public void Dispose()
    {
        _driver.DisposeAsync().AsTask().Wait();
        _store.Dispose();
    }

    [Fact]
    public async Task StartAsync_ConnectsDriver()
    {
        Assert.False(_driver.IsConnected);

        await _manager.StartAsync(CancellationToken.None);

        Assert.True(_driver.IsConnected);
        Assert.Equal(1, _driver.ConnectCount);
    }

    [Fact]
    public async Task StartAsync_LogsWarning_WhenConnectionFails()
    {
        var expectedException = new InvalidOperationException("BLE device not found");
        _driver.ConnectException = expectedException;

        await _manager.StartAsync(CancellationToken.None);

        Assert.False(_driver.IsConnected);
        Assert.Equal(1, _driver.ConnectCount);
        var warning = Assert.Single(_logger.Entries, e => e.LogLevel == LogLevel.Warning);
        Assert.Same(expectedException, warning.Exception);
    }

    [Fact]
    public async Task StopAsync_DisconnectsDriver()
    {
        await _manager.StartAsync(CancellationToken.None);

        await _manager.StopAsync(CancellationToken.None);

        Assert.False(_driver.IsConnected);
        Assert.Equal(1, _driver.DisconnectCount);
    }

    [Theory]
    [InlineData(AgentEvent.SessionStart, AgentCoreLightCommand.Idle)]
    [InlineData(AgentEvent.UserPromptSubmit, AgentCoreLightCommand.Thinking)]
    [InlineData(AgentEvent.PreToolUse, AgentCoreLightCommand.Busy)]
    [InlineData(AgentEvent.PostToolUse, AgentCoreLightCommand.Ai)]
    [InlineData(AgentEvent.PermissionRequest, AgentCoreLightCommand.WaitConfirm)]
    [InlineData(AgentEvent.Stop, AgentCoreLightCommand.Success)]
    [InlineData(AgentEvent.StopFailure, AgentCoreLightCommand.Error)]
    [InlineData(AgentEvent.Disconnect, AgentCoreLightCommand.Off)]
    [InlineData(AgentEvent.SessionEnd, AgentCoreLightCommand.Off)]
    public async Task OnAgentEventAsync_WhenMaster_MapsEventToCommand(AgentEvent agentEvent, AgentCoreLightCommand expectedCommand)
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
    public async Task OnAgentEventAsync_DoesNotConnectDriver()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);
        _store.TrySetMaster("agent-1");
        Assert.False(_driver.IsConnected);

        await _manager.OnAgentEventAsync("agent-1", AgentEvent.SessionStart, CancellationToken.None);

        Assert.False(_driver.IsConnected);
        Assert.Single(_driver.Commands);
    }
}
