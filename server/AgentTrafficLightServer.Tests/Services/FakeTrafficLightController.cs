using AgentTrafficLight.Server.Services;

namespace AgentTrafficLight.Server.Tests.Services;

/// <summary>
/// In-memory fake of <see cref="ITrafficLightController"/> for integration tests.
/// </summary>
public sealed class FakeTrafficLightController : ITrafficLightController
{
    private readonly List<TrafficLightState> _states = new();

    /// <inheritdoc />
    public TrafficLightState CurrentState { get; private set; } = TrafficLightState.Off;

    /// <summary>
    /// Gets or sets a value indicating whether the fake reports a live connection.
    /// </summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>
    /// Gets the ordered list of states written to the fake controller.
    /// </summary>
    public IReadOnlyList<TrafficLightState> States => _states.AsReadOnly();

    /// <summary>
    /// Gets the number of times <see cref="SetStateAsync"/> was called.
    /// </summary>
    public int SetStateCallCount { get; set; }

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
    public Task SetStateAsync(TrafficLightState state, CancellationToken cancellationToken = default)
    {
        if (state == CurrentState)
        {
            return Task.CompletedTask;
        }

        SetStateCallCount++;
        CurrentState = state;
        _states.Add(state);
        return Task.CompletedTask;
    }
}
