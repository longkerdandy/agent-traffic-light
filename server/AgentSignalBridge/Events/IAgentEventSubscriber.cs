using AgentSignalBridge.Server.Models;

namespace AgentSignalBridge.Server.Events;

/// <summary>
/// Subscriber that reacts to agent lifecycle events.
/// </summary>
public interface IAgentEventSubscriber
{
    /// <summary>
    /// Called when an agent lifecycle event occurs.
    /// </summary>
    /// <param name="agentId">The agent identifier that triggered the event.</param>
    /// <param name="agentEvent">The agent event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnAgentEventAsync(string agentId, AgentEvent agentEvent, CancellationToken cancellationToken = default);
}
