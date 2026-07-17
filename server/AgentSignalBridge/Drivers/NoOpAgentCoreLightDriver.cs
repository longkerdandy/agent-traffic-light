namespace AgentSignalBridge.Server.Drivers;

/// <summary>
/// No-op implementation of <see cref="IAgentCoreLightDriver"/> used when BLE hardware is disabled.
/// All commands are accepted and discarded, and the driver reports itself as disconnected.
/// </summary>
public sealed class NoOpAgentCoreLightDriver : IAgentCoreLightDriver
{
    /// <inheritdoc />
    public bool IsConnected => false;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendCommandAsync(AgentCoreLightCommand command, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
