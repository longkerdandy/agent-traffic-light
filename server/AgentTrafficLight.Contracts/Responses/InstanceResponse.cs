using System.Text.Json.Serialization;
using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Contracts.Responses;

/// <summary>
/// Describes a single active agent instance.
/// </summary>
public sealed class InstanceResponse
{
    /// <summary>
    /// Gets or sets the instance identifier.
    /// </summary>
    [JsonPropertyName("instance_id")]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent kind.
    /// </summary>
    public string Agent { get; set; } = "unknown";

    /// <summary>
    /// Gets or sets the current requested hardware state.
    /// </summary>
    public TrafficLightState State { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the instance holds exclusive control.
    /// </summary>
    [JsonPropertyName("is_controller")]
    public bool IsController { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last client contact.
    /// </summary>
    [JsonPropertyName("last_seen")]
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }
}
