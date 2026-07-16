using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Server.Models;

/// <summary>
/// Represents a single running agent client instance.
/// </summary>
public sealed class AgentInstance
{
    /// <summary>
    /// Gets the instance identifier.
    /// </summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent kind (e.g., "kimi", "claude").
    /// </summary>
    public string Agent { get; set; } = "unknown";

    /// <summary>
    /// Gets or sets the working directory of the agent process.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets the current requested hardware state.
    /// </summary>
    public TrafficLightState State { get; set; } = TrafficLightState.Off;

    /// <summary>
    /// Gets or sets a value indicating whether the instance currently holds exclusive control.
    /// </summary>
    public bool IsController { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last client contact.
    /// </summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
