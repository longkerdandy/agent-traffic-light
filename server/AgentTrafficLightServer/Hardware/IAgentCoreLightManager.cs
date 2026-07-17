using AgentTrafficLight.Contracts.Drivers;

namespace AgentTrafficLight.Server.Hardware;

/// <summary>
/// Exposes the current state of the bound hardware manager.
/// </summary>
public interface IAgentCoreLightManager
{
    /// <summary>
    /// Gets the last command sent to the hardware, if any.
    /// </summary>
    AgentCoreLightCommand? LastCommand { get; }

    /// <summary>
    /// Gets the time of the last command update.
    /// </summary>
    DateTimeOffset? LastUpdated { get; }

    /// <summary>
    /// Gets a value indicating whether the hardware driver is currently connected.
    /// </summary>
    bool IsConnected { get; }
}
