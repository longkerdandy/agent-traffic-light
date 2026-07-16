using AgentTrafficLight.Server.Events;
using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Hardware;

/// <summary>
/// No-op hardware manager used for headless environments or when no hardware is available.
/// </summary>
public sealed class NullTrafficLightManager : IAgentEventSubscriber
{
    /// <inheritdoc />
    public Task OnAgentEventAsync(string agentId, AgentEvent agentEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
