using AgentTrafficLight.Server.Models;
using AgentTrafficLight.Server.Services;

namespace AgentTrafficLight.Server.Tests.Services;

public sealed class InMemoryAgentStoreTests : IDisposable
{
    private readonly InMemoryAgentStore _store = new(TimeSpan.FromMilliseconds(50));

    public void Dispose()
    {
        _store.Dispose();
    }

    [Fact]
    public void Upsert_CreatesNewAgent_WhenNotExists()
    {
        var now = DateTimeOffset.UtcNow;

        var agent = _store.Upsert("agent-1", "kimi", "/tmp", AgentState.Idle, now);

        Assert.Equal("agent-1", agent.AgentId);
        Assert.Equal("kimi", agent.AgentName);
        Assert.Equal("/tmp", agent.Cwd);
        Assert.Equal(AgentState.Idle, agent.State);
        Assert.Equal(now, agent.LastSeen);
        Assert.False(agent.IsController);
    }

    [Fact]
    public void Upsert_UpdatesExistingAgent()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", "/tmp", AgentState.Idle, now);

        var later = now.AddSeconds(10);
        var updated = _store.Upsert("agent-1", "claude", "/other", AgentState.Busy, later);

        Assert.Equal("claude", updated.AgentName);
        Assert.Equal("/other", updated.Cwd);
        Assert.Equal(AgentState.Busy, updated.State);
        Assert.Equal(later, updated.LastSeen);
    }

    [Fact]
    public void Touch_CreatesAgentWithOffState_WhenNotExists()
    {
        var now = DateTimeOffset.UtcNow;

        var agent = _store.Touch("agent-1", "kimi", "/tmp", now);

        Assert.Equal("agent-1", agent.AgentId);
        Assert.Equal("kimi", agent.AgentName);
        Assert.Equal("/tmp", agent.Cwd);
        Assert.Equal(AgentState.Off, agent.State);
        Assert.Equal(now, agent.LastSeen);
        Assert.False(agent.IsController);
    }

    [Fact]
    public void Touch_PreservesStateAndUpdatesLastSeen_WhenExists()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", "/tmp", AgentState.Busy, now);

        var later = now.AddSeconds(10);
        var touched = _store.Touch("agent-1", "claude", "/other", later);

        Assert.Equal("claude", touched.AgentName);
        Assert.Equal("/other", touched.Cwd);
        Assert.Equal(AgentState.Busy, touched.State);
        Assert.Equal(later, touched.LastSeen);
    }

    [Fact]
    public async Task Touch_ResetsTtl()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);

        await Task.Delay(TimeSpan.FromMilliseconds(30));
        _store.Touch("agent-1", "kimi", null, now.AddMilliseconds(30));

        await Task.Delay(TimeSpan.FromMilliseconds(30));
        Assert.True(_store.TryGet("agent-1", out _));

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        Assert.False(_store.TryGet("agent-1", out _));
    }

    [Fact]
    public void TryGet_ReturnsAgent_WhenExists()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);

        var found = _store.TryGet("agent-1", out var agent);

        Assert.True(found);
        Assert.NotNull(agent);
        Assert.Equal("agent-1", agent.AgentId);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenNotExists()
    {
        var found = _store.TryGet("missing", out var agent);

        Assert.False(found);
        Assert.Null(agent);
    }

    [Fact]
    public void TryRemove_RemovesExistingAgent()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);

        var removed = _store.TryRemove("agent-1", out var agent);

        Assert.True(removed);
        Assert.NotNull(agent);
        Assert.False(_store.TryGet("agent-1", out _));
    }

    [Fact]
    public void TryRemove_ReturnsFalse_WhenNotExists()
    {
        var removed = _store.TryRemove("missing", out var agent);

        Assert.False(removed);
        Assert.Null(agent);
    }

    [Fact]
    public void GetSnapshot_ReturnsAllActiveAgents()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);
        _store.Upsert("agent-2", "claude", null, AgentState.Busy, now);

        var snapshot = _store.GetSnapshot();

        Assert.Equal(2, snapshot.Count);
    }

    [Fact]
    public void TrySetController_Succeeds_WhenControlIsFree()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);

        var claimed = _store.TrySetController("agent-1");

        Assert.True(claimed);
        Assert.Equal("agent-1", _store.GetControllerAgentId());
        Assert.True(_store.TryGet("agent-1", out var agent) && agent!.IsController);
    }

    [Fact]
    public void TrySetController_Fails_WhenAnotherAgentControls()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);
        _store.Upsert("agent-2", "claude", null, AgentState.Busy, now);
        _store.TrySetController("agent-1");

        var claimed = _store.TrySetController("agent-2");

        Assert.False(claimed);
        Assert.Equal("agent-1", _store.GetControllerAgentId());
    }

    [Fact]
    public void TryReleaseController_ReleasesControl_WhenAgentIsController()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);
        _store.TrySetController("agent-1");

        var released = _store.TryReleaseController("agent-1");

        Assert.True(released);
        Assert.Null(_store.GetControllerAgentId());
        Assert.True(_store.TryGet("agent-1", out var agent) && !agent!.IsController);
    }

    [Fact]
    public async Task Timer_RemovesAgent_WhenTtlExpires()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);

        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Assert.False(_store.TryGet("agent-1", out _));
    }

    [Fact]
    public async Task Timer_Resets_WhenAgentIsUpsertedAgain()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);

        await Task.Delay(TimeSpan.FromMilliseconds(30));
        _store.Upsert("agent-1", "kimi", null, AgentState.Thinking, now.AddMilliseconds(30));

        await Task.Delay(TimeSpan.FromMilliseconds(30));
        Assert.True(_store.TryGet("agent-1", out _));

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        Assert.False(_store.TryGet("agent-1", out _));
    }

    [Fact]
    public async Task Timer_ReleasesController_WhenControllerAgentExpires()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);
        _store.TrySetController("agent-1");

        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Assert.Null(_store.GetControllerAgentId());
    }

    [Fact]
    public void Dispose_PreventsFurtherAccess()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentState.Idle, now);

        _store.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _store.TryGet("agent-1", out _));
    }
}
