using AgentTrafficLight.Server.Events;
using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Tests.Events;

public sealed class AgentEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_NotifiesAllSubscribers()
    {
        var first = new FakeSubscriber();
        var second = new FakeSubscriber();
        var dispatcher = new AgentEventDispatcher([first, second]);

        await dispatcher.DispatchAsync("agent-1", AgentEvent.SessionStart, CancellationToken.None);

        Assert.Equal([("agent-1", AgentEvent.SessionStart)], first.Events);
        Assert.Equal([("agent-1", AgentEvent.SessionStart)], second.Events);
    }

    [Fact]
    public async Task DispatchAsync_NotifiesSubscribersInOrder()
    {
        var first = new FakeSubscriber();
        var second = new FakeSubscriber();
        var dispatcher = new AgentEventDispatcher([first, second]);

        await dispatcher.DispatchAsync("agent-1", AgentEvent.PreToolUse, CancellationToken.None);
        await dispatcher.DispatchAsync("agent-1", AgentEvent.PostToolUse, CancellationToken.None);

        Assert.Equal([("agent-1", AgentEvent.PreToolUse), ("agent-1", AgentEvent.PostToolUse)], first.Events);
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
