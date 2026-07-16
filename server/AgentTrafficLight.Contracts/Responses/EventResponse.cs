using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Contracts.Responses;

/// <summary>
/// Response returned after an instance requests a hardware state.
/// </summary>
public sealed class EventResponse : BaseResponse
{
    /// <summary>
    /// Gets or sets the instance identifier.
    /// </summary>
    public string? InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the requested hardware state.
    /// </summary>
    public TrafficLightState State { get; set; }
}
