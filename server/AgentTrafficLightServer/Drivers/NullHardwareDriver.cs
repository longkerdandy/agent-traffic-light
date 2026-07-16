namespace AgentTrafficLight.Server.Drivers;

/// <summary>
/// No-op driver used for development, testing, or headless environments.
/// </summary>
public sealed class NullHardwareDriver : IHardwareDriver
{
    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
    public TrafficLightCommand CurrentCommand { get; private set; } = TrafficLightCommand.Off;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendCommandAsync(TrafficLightCommand command, CancellationToken cancellationToken = default)
    {
        CurrentCommand = command;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
