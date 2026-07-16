using AgentTrafficLight.Contracts.Models;

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
    /// Gets a value indicating whether the controller is connected to the hardware.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the hardware.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the hardware.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the traffic-light state and writes the corresponding command to the hardware.
    /// </summary>
    /// <param name="state">The desired state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetStateAsync(TrafficLightState state, CancellationToken cancellationToken = default);
}
