using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Contracts.Responses;

/// <summary>
/// Response returned after connecting an instance.
/// </summary>
public sealed class ConnectResponse : BaseResponse
{
    /// <summary>
    /// Gets or sets the instance identifier.
    /// </summary>
    public string? InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the current state of the instance.
    /// </summary>
    public TrafficLightState State { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the instance expires if not refreshed.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
