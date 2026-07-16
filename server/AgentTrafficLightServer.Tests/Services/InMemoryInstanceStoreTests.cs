namespace AgentTrafficLight.Server.Tests.Services;

public sealed class InMemoryInstanceStoreTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(120);

    [Fact]
    public void Upsert_CreatesNewInstance_WithOffState()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;

        var instance = store.Upsert("id-1", "kimi", "/tmp", now, Ttl);

        Assert.Equal("id-1", instance.InstanceId);
        Assert.Equal("kimi", instance.Agent);
        Assert.Equal("/tmp", instance.Cwd);
        Assert.Equal(TrafficLightState.Off, instance.State);
        Assert.False(instance.IsController);
        Assert.Equal(now, instance.LastSeen);
        Assert.Equal(now + Ttl, instance.ExpiresAt);
    }

    [Fact]
    public void Upsert_RefreshesExistingInstance_WithoutChangingState()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;
        var instance = store.Upsert("id-1", "kimi", "/tmp", now, Ttl);
        _ = store.SetState("id-1", "kimi", "/tmp", TrafficLightState.Thinking, now, Ttl);

        var later = now + TimeSpan.FromMinutes(1);
        var refreshed = store.Upsert("id-1", "claude", "/other", later, Ttl);

        Assert.Same(instance, refreshed);
        Assert.Equal("claude", refreshed.Agent);
        Assert.Equal("/other", refreshed.Cwd);
        Assert.Equal(TrafficLightState.Thinking, refreshed.State);
        Assert.Equal(later, refreshed.LastSeen);
        Assert.Equal(later + Ttl, refreshed.ExpiresAt);
    }

    [Fact]
    public void SetState_CreatesInstance_WhenMissing()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;

        var instance = store.SetState("id-1", "kimi", "/tmp", TrafficLightState.Busy, now, Ttl);

        Assert.Equal(TrafficLightState.Busy, instance.State);
        Assert.True(store.TryGet("id-1", out _));
    }

    [Fact]
    public void Touch_CreatesInstance_WhenMissing()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;

        var instance = store.Touch("id-1", "kimi", "/tmp", now, Ttl);

        Assert.Equal(TrafficLightState.Off, instance.State);
        Assert.True(store.TryGet("id-1", out _));
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForExpiredInstance()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;
        _ = store.Upsert("id-1", "kimi", null, now, TimeSpan.FromSeconds(-1));

        var found = store.TryGet("id-1", out var instance);

        Assert.False(found);
        Assert.Null(instance);
    }

    [Fact]
    public void TrySetController_Succeeds_WhenControlIsFree()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;
        _ = store.Upsert("id-1", "kimi", null, now, Ttl);

        var claimed = store.TrySetController("id-1", out var conflict);

        Assert.True(claimed);
        Assert.Null(conflict);
        Assert.Equal("id-1", store.GetControllerInstanceId());
        Assert.True(store.TryGet("id-1", out var instance) && instance.IsController);
    }

    [Fact]
    public void TrySetController_IsIdempotent_ForCurrentController()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;
        _ = store.Upsert("id-1", "kimi", null, now, Ttl);
        Assert.True(store.TrySetController("id-1", out _));

        var claimed = store.TrySetController("id-1", out var conflict);

        Assert.True(claimed);
        Assert.Null(conflict);
    }

    [Fact]
    public void TrySetController_Fails_WhenAnotherInstanceHoldsControl()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;
        _ = store.Upsert("id-1", "kimi", null, now, Ttl);
        _ = store.Upsert("id-2", "claude", null, now, Ttl);
        Assert.True(store.TrySetController("id-1", out _));

        var claimed = store.TrySetController("id-2", out var conflict);

        Assert.False(claimed);
        Assert.Equal("id-1", conflict);
        Assert.Equal("id-1", store.GetControllerInstanceId());
    }

    [Fact]
    public void TrySetController_Fails_WhenInstanceDoesNotExist()
    {
        var store = new InMemoryInstanceStore();

        var claimed = store.TrySetController("missing", out var conflict);

        Assert.False(claimed);
        Assert.Null(conflict);
    }

    [Fact]
    public void TryReleaseController_ReleasesControl()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;
        _ = store.Upsert("id-1", "kimi", null, now, Ttl);
        Assert.True(store.TrySetController("id-1", out _));

        var released = store.TryReleaseController("id-1");

        Assert.True(released);
        Assert.Null(store.GetControllerInstanceId());
        Assert.True(store.TryGet("id-1", out var instance) && !instance.IsController);
    }

    [Fact]
    public void TryReleaseController_ReturnsFalse_WhenNotController()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;
        _ = store.Upsert("id-1", "kimi", null, now, Ttl);

        var released = store.TryReleaseController("id-1");

        Assert.False(released);
    }

    [Fact]
    public void TryRemove_ReleasesControl_WhenControllerRemoved()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;
        _ = store.Upsert("id-1", "kimi", null, now, Ttl);
        Assert.True(store.TrySetController("id-1", out _));

        var removed = store.TryRemove("id-1", out var instance);

        Assert.True(removed);
        Assert.NotNull(instance);
        Assert.False(instance.IsController);
        Assert.Null(store.GetControllerInstanceId());
        Assert.False(store.TryGet("id-1", out _));
    }

    [Fact]
    public void RemoveExpiredInstances_RemovesController_AndReportsIt()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;
        _ = store.Upsert("id-1", "kimi", null, now, Ttl);
        _ = store.Upsert("id-2", "claude", null, now, Ttl);
        Assert.True(store.TrySetController("id-1", out _));

        var later = now + Ttl + TimeSpan.FromSeconds(1);
        var controllerRemoved = store.RemoveExpiredInstances(later);

        Assert.True(controllerRemoved);
        Assert.Null(store.GetControllerInstanceId());
        Assert.False(store.TryGet("id-1", out _));
        Assert.False(store.TryGet("id-2", out _));
    }

    [Fact]
    public void GetSnapshot_ExcludesExpiredInstances()
    {
        var store = new InMemoryInstanceStore();
        var now = DateTimeOffset.UtcNow;
        _ = store.Upsert("id-1", "kimi", null, now, TimeSpan.FromSeconds(-1));
        _ = store.Upsert("id-2", "claude", null, now, Ttl);

        var snapshot = store.GetSnapshot();

        Assert.Single(snapshot);
        Assert.Equal("id-2", snapshot[0].InstanceId);
    }
}
