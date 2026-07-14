namespace AgentTrafficLight.Server.Tests.Fakes;

/// <summary>
/// In-memory fake of <see cref="ISerialController"/> for unit tests.
/// </summary>
public sealed class FakeSerialController : ISerialController
{
    /// <summary>
    /// Gets or sets a value indicating whether the controller reports as connected.
    /// </summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>
    /// Gets the list of commands that have been written.
    /// </summary>
    public List<string> Commands { get; } = new();

    /// <summary>
    /// Records the command and completes synchronously.
    /// </summary>
    /// <param name="command">The command to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task WriteAsync(string command, CancellationToken cancellationToken = default)
    {
        Commands.Add(command);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
