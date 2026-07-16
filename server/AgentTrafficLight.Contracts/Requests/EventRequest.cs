using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Contracts.Requests;

/// <summary>
/// Request body for requesting a specific hardware state for an instance.
/// </summary>
public sealed class EventRequest
{
    /// <summary>
    /// Gets or sets the requested hardware state.
    /// </summary>
    public TrafficLightState State { get; set; }
}
