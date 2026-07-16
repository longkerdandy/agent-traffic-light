namespace AgentTrafficLight.Server.Drivers;

/// <summary>
/// Abstraction for a physical traffic-light driver.
/// </summary>
public interface IHardwareDriver : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the driver is connected to the hardware.
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
    /// Sends a traffic-light command to the hardware.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendCommandAsync(TrafficLightCommand command, CancellationToken cancellationToken = default);
}
