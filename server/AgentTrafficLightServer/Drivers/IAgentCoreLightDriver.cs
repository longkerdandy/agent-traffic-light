namespace AgentTrafficLight.Server.Drivers;

/// <summary>
/// Driver abstraction for AgentCore-Light compatible hardware.
/// </summary>
public interface IAgentCoreLightDriver : IAsyncDisposable
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
    /// Sends an AgentCoreLight command to the hardware.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendCommandAsync(AgentCoreLightCommand command, CancellationToken cancellationToken = default);
}
