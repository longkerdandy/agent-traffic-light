using AgentTrafficLight.Server.Drivers;

namespace AgentTrafficLight.Server.Tests.Fakes;

/// <summary>
/// Fake AgentCore-Light driver for testing.
/// </summary>
public sealed class FakeAgentCoreLightDriver : IAgentCoreLightDriver
{
    private readonly List<TrafficLightCommand> _commands = [];

    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Gets the list of commands sent to the driver.
    /// </summary>
    public IReadOnlyList<TrafficLightCommand> Commands => _commands.AsReadOnly();

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
        _commands.Add(command);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Clears the recorded commands.
    /// </summary>
    public void ClearCommands()
    {
        _commands.Clear();
    }
}
