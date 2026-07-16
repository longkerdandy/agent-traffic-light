using System.Text.Json.Serialization;
using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Contracts.Responses;

/// <summary>
/// Response returned by the list-instances endpoint.
/// </summary>
public sealed class InstancesResponse : BaseResponse
{
    /// <summary>
    /// Gets or sets the current hardware state.
    /// </summary>
    public TrafficLightState State { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the current controller, or <see langword="null"/>.
    /// </summary>
    [JsonPropertyName("controller_instance_id")]
    public string? ControllerInstanceId { get; set; }

    /// <summary>
    /// Gets or sets the active instances.
    /// </summary>
    public IReadOnlyList<InstanceResponse> Instances { get; set; } = Array.Empty<InstanceResponse>();
}
