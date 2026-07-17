using AgentSignalBridge.Server.Utils;

namespace AgentSignalBridge.Server.Tests.Utils;

public sealed class EventAsyncTests
{
    [Fact]
    public async Task WaitForEventAsync_Completes_WhenEventIsRaised()
    {
        var source = new EventSource();

        var task = EventAsync.WaitForEventAsync<EventArgs>(
            h => source.Event += h,
            h => source.Event -= h,
            cancellationToken: default);

        source.Raise();

        var args = await task;
        Assert.NotNull(args);
    }

    [Fact]
    public async Task WaitForEventAsync_UsesPredicate_ToFilterEvents()
    {
        var source = new FilterableEventSource();

        var task = EventAsync.WaitForEventAsync<FilterableEventArgs>(
            h => source.Event += h,
            h => source.Event -= h,
            args => args.Value == 42);

        source.Raise(1);
        source.Raise(2);
        source.Raise(42);

        var args = await task;
        Assert.Equal(42, args.Value);
    }

    [Fact]
    public async Task WaitForEventAsync_Throws_WhenCanceled()
    {
        var source = new EventSource();
        using var cts = new CancellationTokenSource();

        var task = EventAsync.WaitForEventAsync<EventArgs>(
            h => source.Event += h,
            h => source.Event -= h,
            cancellationToken: cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task WaitForEventAsync_UnsubscribesHandler_AfterCompletion()
    {
        var source = new EventSource();

        var task = EventAsync.WaitForEventAsync<EventArgs>(
            h => source.Event += h,
            h => source.Event -= h);

        source.Raise();
        await task;

        // After completion the handler should be removed, so raising again
        // should not change the already-completed task.
        source.Raise();
        Assert.True(task.IsCompletedSuccessfully);
    }

    private sealed class EventSource
    {
        public event EventHandler<EventArgs>? Event;

        public void Raise() => Event?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FilterableEventSource
    {
        public event EventHandler<FilterableEventArgs>? Event;

        public void Raise(int value) => Event?.Invoke(this, new FilterableEventArgs(value));
    }

    private sealed class FilterableEventArgs : EventArgs
    {
        public FilterableEventArgs(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }
}
