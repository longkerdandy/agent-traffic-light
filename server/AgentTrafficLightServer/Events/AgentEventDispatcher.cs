using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Server.Events;

/// <summary>
/// Dispatches agent lifecycle events to all registered subscribers.
/// </summary>
public sealed class AgentEventDispatcher
{
    private readonly IEnumerable<IAgentEventSubscriber> _subscribers;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentEventDispatcher"/> class.
    /// </summary>
    /// <param name="subscribers">The registered event subscribers.</param>
    public AgentEventDispatcher(IEnumerable<IAgentEventSubscriber> subscribers)
    {
        _subscribers = subscribers;
    }

    /// <summary>
    /// Dispatches an agent event to all subscribers.
    /// </summary>
    /// <param name="agentId">The agent identifier that triggered the event.</param>
    /// <param name="agentEvent">The agent event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DispatchAsync(string agentId, AgentEvent agentEvent, CancellationToken cancellationToken = default)
    {
        foreach (var subscriber in _subscribers)
        {
            await subscriber.OnAgentEventAsync(agentId, agentEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
