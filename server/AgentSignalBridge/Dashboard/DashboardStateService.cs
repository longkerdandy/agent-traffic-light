using AgentSignalBridge.Server.Api;
using AgentSignalBridge.Server.Events;
using AgentSignalBridge.Server.Models;
using AgentSignalBridge.Server.Services;
using AgentSignalBridge.Server.Stores;

namespace AgentSignalBridge.Server.Dashboard;

/// <summary>
/// Bridges server-side state changes to the Blazor dashboard.
/// Subscribes to <see cref="StateChangeNotifier"/> and exposes helpers
/// for the manual control panel to send events and claim or release master control.
/// </summary>
public sealed class DashboardStateService : IDisposable
{
    private readonly AgentLifecycleService _lifecycleService;
    private readonly IAgentStore _store;
    private readonly StateChangeNotifier _notifier;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _reader;

    /// <summary>
    /// Raised when the server state changes and dashboard components should refresh.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardStateService"/> class.
    /// </summary>
    /// <param name="lifecycleService">The agent lifecycle service used to process events.</param>
    /// <param name="store">The agent session store.</param>
    /// <param name="notifier">The state change notifier.</param>
    /// <param name="timeProvider">The time provider.</param>
    public DashboardStateService(
        AgentLifecycleService lifecycleService,
        IAgentStore store,
        StateChangeNotifier notifier,
        TimeProvider timeProvider)
    {
        _lifecycleService = lifecycleService;
        _store = store;
        _notifier = notifier;
        _timeProvider = timeProvider;
        _reader = Task.Run(() => ReadLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Sends a canonical agent lifecycle event through the same pipeline used by <c>POST /hook</c>.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="agentEvent">The lifecycle event.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendEventAsync(string agentId, string agentName, AgentEvent agentEvent)
    {
        await _lifecycleService.ProcessEventAsync(
            agentId,
            agentName,
            cwd: null,
            agentEvent,
            _timeProvider.GetUtcNow()).ConfigureAwait(false);
    }

    /// <summary>
    /// Claims or releases master control for the specified agent through the same store used by <c>POST /api/master</c>.
    /// Publishes a state change when the master role actually changes.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="enabled"><see langword="true"/> to claim master; <see langword="false"/> to release.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task SetMasterAsync(string agentId, bool enabled)
    {
        var changed = enabled ? _store.TrySetMaster(agentId) : _store.TryReleaseMaster(agentId);

        if (changed)
        {
            _notifier.Publish(
                new StateChangedEvent
                {
                    Command = null,
                    MasterAgentId = _store.GetMasterAgentId(),
                    Timestamp = _timeProvider.GetUtcNow()
                });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            _reader.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Expected when the cancellation token stops the reader.
        }

        _cts.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var _ in _notifier.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                OnChange?.Invoke();
            }
            catch
            {
                // Ignore subscriber exceptions so one failing component does not break the loop.
            }
        }
    }
}
