using AgentSignalBridge.Server.Models;
using AgentSignalBridge.Server.Stores;

namespace AgentSignalBridge.Server.Tests.Stores;

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

        var agent = _store.Upsert("agent-1", "kimi", "/tmp", AgentEvent.SessionStart, now);

        Assert.Equal("agent-1", agent.AgentId);
        Assert.Equal("kimi", agent.AgentName);
        Assert.Equal("/tmp", agent.Cwd);
        Assert.Equal(AgentEvent.SessionStart, agent.Event);
        Assert.Equal(now, agent.LastSeen);
        Assert.False(agent.IsMaster);
    }

    [Fact]
    public void Upsert_UpdatesExistingAgent()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", "/tmp", AgentEvent.SessionStart, now);

        var later = now.AddSeconds(10);
        var updated = _store.Upsert("agent-1", "claude", "/other", AgentEvent.PreToolUse, later);

        Assert.Equal("claude", updated.AgentName);
        Assert.Equal("/other", updated.Cwd);
        Assert.Equal(AgentEvent.PreToolUse, updated.Event);
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
        Assert.Equal(AgentEvent.Disconnect, agent.Event);
        Assert.Equal(now, agent.LastSeen);
        Assert.False(agent.IsMaster);
    }

    [Fact]
    public void Touch_PreservesStateAndUpdatesLastSeen_WhenExists()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", "/tmp", AgentEvent.PreToolUse, now);

        var later = now.AddSeconds(10);
        var touched = _store.Touch("agent-1", "claude", "/other", later);

        Assert.Equal("claude", touched.AgentName);
        Assert.Equal("/other", touched.Cwd);
        Assert.Equal(AgentEvent.PreToolUse, touched.Event);
        Assert.Equal(later, touched.LastSeen);
    }

    [Fact]
    public async Task Touch_ResetsTtl()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);

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
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);

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
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);

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
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);
        _store.Upsert("agent-2", "claude", null, AgentEvent.PreToolUse, now);

        var snapshot = _store.GetSnapshot();

        Assert.Equal(2, snapshot.Count);
    }

    [Fact]
    public void TrySetMaster_Succeeds_WhenControlIsFree()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);

        var claimed = _store.TrySetMaster("agent-1");

        Assert.True(claimed);
        Assert.Equal("agent-1", _store.GetMasterAgentId());
        Assert.True(_store.TryGet("agent-1", out var agent) && agent!.IsMaster);
    }

    [Fact]
    public void TrySetMaster_Fails_WhenAnotherAgentControls()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);
        _store.Upsert("agent-2", "claude", null, AgentEvent.PreToolUse, now);
        _store.TrySetMaster("agent-1");

        var claimed = _store.TrySetMaster("agent-2");

        Assert.False(claimed);
        Assert.Equal("agent-1", _store.GetMasterAgentId());
    }

    [Fact]
    public void TryReleaseMaster_ReleasesControl_WhenAgentIsMaster()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);
        _store.TrySetMaster("agent-1");

        var released = _store.TryReleaseMaster("agent-1");

        Assert.True(released);
        Assert.Null(_store.GetMasterAgentId());
        Assert.True(_store.TryGet("agent-1", out var agent) && !agent!.IsMaster);
    }

    [Fact]
    public async Task Timer_RemovesAgent_WhenTtlExpires()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);

        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Assert.False(_store.TryGet("agent-1", out _));
    }

    [Fact]
    public async Task Timer_Resets_WhenAgentIsUpsertedAgain()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);

        await Task.Delay(TimeSpan.FromMilliseconds(30));
        _store.Upsert("agent-1", "kimi", null, AgentEvent.PreToolUse, now.AddMilliseconds(30));

        await Task.Delay(TimeSpan.FromMilliseconds(30));
        Assert.True(_store.TryGet("agent-1", out _));

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        Assert.False(_store.TryGet("agent-1", out _));
    }

    [Fact]
    public async Task Timer_ReleasesMaster_WhenMasterAgentExpires()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);
        _store.TrySetMaster("agent-1");

        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Assert.Null(_store.GetMasterAgentId());
    }

    [Fact]
    public void Dispose_PreventsFurtherAccess()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Upsert("agent-1", "kimi", null, AgentEvent.SessionStart, now);

        _store.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _store.TryGet("agent-1", out _));
    }
}
