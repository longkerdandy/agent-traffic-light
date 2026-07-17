using AgentSignalBridge.Server.Events;
using AgentSignalBridge.Server.Models;
using AgentSignalBridge.Server.Services;
using AgentSignalBridge.Server.Stores;

namespace AgentSignalBridge.Server.Tests.Services;

public sealed class AgentLifecycleServiceTests : IDisposable
{
    private readonly InMemoryAgentStore _store = new();
    private readonly AgentEventDispatcher _dispatcher;
    private readonly FakeSubscriber _subscriber = new();
    private readonly AgentLifecycleService _service;

    public AgentLifecycleServiceTests()
    {
        _dispatcher = new AgentEventDispatcher([_subscriber]);
        _service = new AgentLifecycleService(_store, _dispatcher);
    }

    public void Dispose()
    {
        _store.Dispose();
    }

    [Fact]
    public async Task ProcessEventAsync_UpsertsAgentAndDispatchesEvent()
    {
        var now = DateTimeOffset.UtcNow;

        await _service.ProcessEventAsync("agent-1", "kimi", "/tmp", AgentEvent.PreToolUse, now, CancellationToken.None);

        Assert.True(_store.TryGet("agent-1", out var agent));
        Assert.NotNull(agent);
        Assert.Equal("agent-1", agent.AgentId);
        Assert.Equal("kimi", agent.AgentName);
        Assert.Equal("/tmp", agent.Cwd);
        Assert.Equal(AgentEvent.PreToolUse, agent.Event);
        Assert.Equal(now, agent.LastSeen);

        Assert.Equal([("agent-1", AgentEvent.PreToolUse)], _subscriber.Events);
    }

    private sealed class FakeSubscriber : IAgentEventSubscriber
    {
        public List<(string AgentId, AgentEvent AgentEvent)> Events { get; } = [];

        public Task OnAgentEventAsync(string agentId, AgentEvent agentEvent, CancellationToken cancellationToken = default)
        {
            Events.Add((agentId, agentEvent));
            return Task.CompletedTask;
        }
    }
}
