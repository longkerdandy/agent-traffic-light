namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Abstraction for writing serial commands to the traffic-light hardware.
/// </summary>
public interface ISerialController : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the serial port is currently connected and open.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Writes a newline-terminated command to the serial port.
    /// </summary>
    /// <param name="command">The command to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task WriteAsync(string command, CancellationToken cancellationToken = default);
}
