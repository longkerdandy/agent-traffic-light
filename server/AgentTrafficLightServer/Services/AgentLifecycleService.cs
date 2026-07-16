using AgentTrafficLight.Server.Events;
using AgentTrafficLight.Server.Models;
using AgentTrafficLight.Server.Stores;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Coordinates agent session state and dispatches lifecycle events to hardware managers.
/// </summary>
public sealed class AgentLifecycleService
{
    private readonly IAgentStore _store;
    private readonly AgentEventDispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentLifecycleService"/> class.
    /// </summary>
    /// <param name="store">The agent session store.</param>
    /// <param name="dispatcher">The agent event dispatcher.</param>
    public AgentLifecycleService(IAgentStore store, AgentEventDispatcher dispatcher)
    {
        _store = store;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Processes an agent lifecycle event, updates the session store, and notifies hardware managers.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="cwd">The working directory, if any.</param>
    /// <param name="agentEvent">The agent lifecycle event.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ProcessEventAsync(
        string agentId,
        string agentName,
        string? cwd,
        AgentEvent agentEvent,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        _store.Upsert(agentId, agentName, cwd, agentEvent, now);
        await _dispatcher.DispatchAsync(agentId, agentEvent, cancellationToken).ConfigureAwait(false);
    }
}
