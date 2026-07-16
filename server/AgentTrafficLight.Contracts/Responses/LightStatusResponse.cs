using System.Text.Json.Serialization;
using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Contracts.Responses;

/// <summary>
/// Response returned by the light-status endpoint.
/// </summary>
public sealed class LightStatusResponse : BaseResponse
{
    /// <summary>
    /// Gets or sets the last state written to the hardware.
    /// </summary>
    public TrafficLightState State { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the controller is connected to the hardware.
    /// </summary>
    [JsonPropertyName("is_connected")]
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the current controller, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("controller_instance_id")]
    public string? ControllerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last successful hardware state change.
    /// </summary>
    [JsonPropertyName("last_updated")]
    public DateTimeOffset LastUpdated { get; set; }
}
