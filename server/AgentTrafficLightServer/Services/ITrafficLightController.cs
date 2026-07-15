using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Controls the AgentCore-Light traffic-light hardware state.
/// </summary>
public interface ITrafficLightController
{
    /// <summary>
    /// Gets the current traffic-light state.
    /// </summary>
    TrafficLightState CurrentState { get; }

    /// <summary>
    /// Sets the traffic-light state and writes the corresponding command to the hardware.
    /// </summary>
    /// <param name="state">The desired state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetStateAsync(TrafficLightState state, CancellationToken cancellationToken = default);
}
