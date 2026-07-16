using AgentTrafficLight.Server.Drivers;

namespace AgentTrafficLight.Server.Tests.Fakes;

/// <summary>
/// Fake AgentCore-Light driver for testing.
/// </summary>
public sealed class FakeAgentCoreLightDriver : IAgentCoreLightDriver
{
    private readonly List<AgentCoreLightCommand> _commands = [];

    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Gets the list of commands sent to the driver.
    /// </summary>
    public IReadOnlyList<AgentCoreLightCommand> Commands => _commands.AsReadOnly();

    /// <summary>
    /// Gets or sets the exception to throw from <see cref="ConnectAsync"/>.
    /// </summary>
    public Exception? ConnectException { get; set; }

    /// <summary>
    /// Gets the number of times <see cref="ConnectAsync"/> has been called.
    /// </summary>
    public int ConnectCount { get; private set; }

    /// <summary>
    /// Gets the number of times <see cref="DisconnectAsync"/> has been called.
    /// </summary>
    public int DisconnectCount { get; private set; }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ConnectCount++;
        if (ConnectException != null)
        {
            return Task.FromException(ConnectException);
        }

        IsConnected = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        DisconnectCount++;
        IsConnected = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendCommandAsync(AgentCoreLightCommand command, CancellationToken cancellationToken = default)
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
